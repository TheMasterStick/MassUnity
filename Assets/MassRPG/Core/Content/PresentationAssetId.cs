using System;

namespace MassRPG.Core.Content
{
    /// <summary>
    /// Stable presentation roles are part of the content contract, not Unity/editor state. Gameplay
    /// may ignore them entirely, while clients map these IDs to whatever local assets they package.
    /// </summary>
    public enum PresentationAssetRole
    {
        Icon,
        Model,
        Portrait,
        AnimationSet
    }

    public static class PresentationAssetId
    {
        public static ContentId For(ContentId contentId, PresentationAssetRole role)
        {
            if (contentId.IsEmpty) throw new ArgumentException("Content ID cannot be empty.", nameof(contentId));
            return new ContentId("asset/" + RoleSegment(role) + "/" + contentId.Value);
        }

        public static string RoleSegment(PresentationAssetRole role)
        {
            switch (role)
            {
                case PresentationAssetRole.Icon: return "icon";
                case PresentationAssetRole.Model: return "model";
                case PresentationAssetRole.Portrait: return "portrait";
                case PresentationAssetRole.AnimationSet: return "animation";
                default: throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        }

        public static bool TryParse(ContentId assetId, out PresentationAssetRole role, out ContentId contentId)
        {
            role = default;
            contentId = default;
            var value = assetId.Value;
            if (!value.StartsWith("asset/", StringComparison.Ordinal)) return false;

            var roleEnd = value.IndexOf('/', 6);
            if (roleEnd < 0 || roleEnd == value.Length - 1) return false;
            var roleText = value.Substring(6, roleEnd - 6);
            if (roleText == "icon") role = PresentationAssetRole.Icon;
            else if (roleText == "model") role = PresentationAssetRole.Model;
            else if (roleText == "portrait") role = PresentationAssetRole.Portrait;
            else if (roleText == "animation") role = PresentationAssetRole.AnimationSet;
            else return false;

            return ContentId.TryCreate(value.Substring(roleEnd + 1), out contentId);
        }
    }
}
