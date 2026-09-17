using System;

namespace MassRPG.Core.Authority
{
    public sealed class AuthorityDecision
    {
        private AuthorityDecision(Guid requestId, bool accepted, string code, string message)
        {
            RequestId = requestId;
            Accepted = accepted;
            Code = code;
            Message = message;
        }

        public Guid RequestId { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }

        public static AuthorityDecision Accept(Guid requestId)
            => new AuthorityDecision(requestId, true, "ok", string.Empty);

        public static AuthorityDecision Reject(Guid requestId, string code, string message)
            => new AuthorityDecision(requestId, false, code, message);
    }
}
