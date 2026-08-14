using M1Mentor.Utilities.Exceptions.Common;
using Utilities.Enums;

namespace Utilities.Exceptions
{
    public class AuthorizationException : BaseException
    {
        public AuthorizationException()
           : base(ApiResultStatusCode.UnAuthorized)
        {
        }
        public AuthorizationException(string message)
            : base(ApiResultStatusCode.UnAuthorized, System.Net.HttpStatusCode.Unauthorized, message)
        {
        }
    }
}
