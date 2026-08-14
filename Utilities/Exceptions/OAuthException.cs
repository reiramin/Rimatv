using M1Mentor.Utilities.Exceptions.Common;
using Utilities.Enums;

namespace Utilities.Exceptions
{
    public class OAuthException : BaseException
    {
        public OAuthException()
           : base(ApiResultStatusCode.OAuth)
        {
        }
        public OAuthException(string message)
            : base(ApiResultStatusCode.OAuth, message)
        {
        }
    }
}
