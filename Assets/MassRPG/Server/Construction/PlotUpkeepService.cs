using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Construction;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;

namespace MassRPG.Server.Construction
{
    public enum PlotUpkeepStatus
    {
        Active,
        Delinquent,
        Abandoned
    }

    /// <summary>
    /// Configurable upkeep policy. Exact live gold rates are deliberately data/policy values rather
    /// than hard-coded game rules. Grace duration can likewise be changed without migrating plots.
    /// </summary>
    public sealed class PlotUpkeepPolicy
    {
        public PlotUpkeepPolicy(int smallGoldPerDay, int mediumGoldPerDay, int largeGoldPerDay, TimeSpan gracePeriod)
        {
            if (smallGoldPerDay < 1 || mediumGoldPerDay < 1 || largeGoldPerDay < 1)
                throw new ArgumentOutOfRangeException(nameof(smallGoldPerDay));
            if (gracePeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(gracePeriod));
            SmallGoldPerDay = smallGoldPerDay;
            MediumGoldPerDay = mediumGoldPerDay;
            LargeGoldPerDay = largeGoldPerDay;
            GracePeriod = gracePeriod;
        }

        public int SmallGoldPerDay { get; }
        public int MediumGoldPerDay { get; }
        public int LargeGoldPerDay { get; }
        public TimeSpan GracePeriod { get; }

        public int GoldPerDay(PlotTier tier)
        {
            switch (tier)
            {
                case PlotTier.Small: return SmallGoldPerDay;
                case PlotTier.Medium: return MediumGoldPerDay;
                case PlotTier.Large: return LargeGoldPerDay;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }
    }

    public sealed class PlotUpkeepState
    {
        public PlotUpkeepState(Guid plotId, long paidThroughUnixMilliseconds)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            PlotId = plotId;
            PaidThroughUnixMilliseconds = paidThroughUnixMilliseconds;
        }

        public Guid PlotId { get; }
        public long PaidThroughUnixMilliseconds { get; internal set; }
        public PlotUpkeepStatus Status { get; internal set; } = PlotUpkeepStatus.Active;
        public long? DelinquentSinceUnixMilliseconds { get; internal set; }
        public long? AbandonedAtUnixMilliseconds { get; internal set; }
    }

    public readonly struct PlotUpkeepPaymentResult
    {
        private PlotUpkeepPaymentResult(bool success, string code, int goldSpent, int daysAdded)
        {
            Success = success;
            Code = code ?? string.Empty;
            GoldSpent = goldSpent;
            DaysAdded = daysAdded;
        }

        public bool Success { get; }
        public string Code { get; }
        public int GoldSpent { get; }
        public int DaysAdded { get; }

        public static PlotUpkeepPaymentResult Ok(int goldSpent, int daysAdded)
            => new PlotUpkeepPaymentResult(true, "ok", goldSpent, daysAdded);
        public static PlotUpkeepPaymentResult Fail(string code)
            => new PlotUpkeepPaymentResult(false, code, 0, 0);
    }

    /// <summary>
    /// Prepaid plot upkeep. Once paid time expires a plot becomes delinquent, then abandoned after
    /// the configurable grace period. This service marks lifecycle state only; reclamation/removal
    /// remains an explicit authoritative action so a server never destroys housing accidentally.
    /// </summary>
    public sealed class PlotUpkeepService
    {
        private const long DayMilliseconds = 24L * 60L * 60L * 1000L;
        private static readonly ContentId Coins = new ContentId("coins");

        private readonly PlayerPlotRegistry _plots;
        private readonly PlotUpkeepPolicy _policy;
        private readonly Dictionary<Guid, PlotUpkeepState> _states = new Dictionary<Guid, PlotUpkeepState>();

        public PlotUpkeepService(PlayerPlotRegistry plots, PlotUpkeepPolicy policy)
        {
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public PlotUpkeepState Register(Guid plotId, long initialPaidThroughUnixMilliseconds)
        {
            if (!_plots.TryGet(plotId, out _)) throw new KeyNotFoundException("Unknown plot.");
            if (_states.ContainsKey(plotId)) throw new InvalidOperationException("Plot upkeep is already registered.");
            var state = new PlotUpkeepState(plotId, initialPaidThroughUnixMilliseconds);
            _states.Add(plotId, state);
            return state;
        }

        public bool TryGet(Guid plotId, out PlotUpkeepState state) => _states.TryGetValue(plotId, out state);

        public PlotUpkeepPaymentResult Pay(PlayerState player, Guid plotId, int offeredGold, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_plots.TryGet(plotId, out var plot)) return PlotUpkeepPaymentResult.Fail("unknown_plot");
            if (plot.OwnerCharacterId != player.CharacterId) return PlotUpkeepPaymentResult.Fail("not_owner");
            if (!_states.TryGetValue(plotId, out var state)) return PlotUpkeepPaymentResult.Fail("upkeep_not_registered");
            if (state.Status == PlotUpkeepStatus.Abandoned) return PlotUpkeepPaymentResult.Fail("plot_abandoned");
            if (offeredGold < 1) return PlotUpkeepPaymentResult.Fail("invalid_payment");

            var rate = _policy.GoldPerDay(plot.Tier);
            var days = offeredGold / rate;
            if (days < 1) return PlotUpkeepPaymentResult.Fail("payment_below_one_day");
            var spent = checked(days * rate);
            if (player.Inventory.CountItem(Coins) < spent) return PlotUpkeepPaymentResult.Fail("insufficient_gold");
            if (!InventoryRules.RemoveItem(player.Inventory, Coins, spent))
                throw new InvalidOperationException("Validated upkeep payment could not remove coins.");

            var start = Math.Max(nowUnixMilliseconds, state.PaidThroughUnixMilliseconds);
            state.PaidThroughUnixMilliseconds = checked(start + days * DayMilliseconds);
            state.Status = PlotUpkeepStatus.Active;
            state.DelinquentSinceUnixMilliseconds = null;
            state.AbandonedAtUnixMilliseconds = null;
            return PlotUpkeepPaymentResult.Ok(spent, days);
        }

        public PlotUpkeepStatus Advance(Guid plotId, long nowUnixMilliseconds)
        {
            if (!_states.TryGetValue(plotId, out var state)) throw new KeyNotFoundException("Plot upkeep is not registered.");
            if (state.Status == PlotUpkeepStatus.Abandoned) return state.Status;

            if (nowUnixMilliseconds <= state.PaidThroughUnixMilliseconds)
            {
                state.Status = PlotUpkeepStatus.Active;
                state.DelinquentSinceUnixMilliseconds = null;
                state.AbandonedAtUnixMilliseconds = null;
                return state.Status;
            }

            if (!state.DelinquentSinceUnixMilliseconds.HasValue)
                state.DelinquentSinceUnixMilliseconds = state.PaidThroughUnixMilliseconds;

            var abandonAt = checked(state.PaidThroughUnixMilliseconds + (long)_policy.GracePeriod.TotalMilliseconds);
            state.AbandonedAtUnixMilliseconds = abandonAt;
            state.Status = nowUnixMilliseconds >= abandonAt ? PlotUpkeepStatus.Abandoned : PlotUpkeepStatus.Delinquent;
            return state.Status;
        }
    }
}
