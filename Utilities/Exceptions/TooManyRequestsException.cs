using System.Net;
using M1Mentor.Utilities.Exceptions.Common;
using Utilities.Enums;

namespace Utilities.Exceptions
{
    public class TooManyRequestsException : BaseException
    {
        public TooManyRequestsException()
           : base(ApiResultStatusCode.TooManyRequests)
        {
        }

        public TooManyRequestsException(string message)
            : base(ApiResultStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests, message)
        {
        }
    }
}
