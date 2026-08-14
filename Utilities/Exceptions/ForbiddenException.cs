using System.Net;
using M1Mentor.Utilities.Exceptions.Common;
using Utilities.Enums;

namespace Utilities.Exceptions
{
    public class ForbiddenException : BaseException
    {
        public ForbiddenException()
            : base(ApiResultStatusCode.Forbidden)
        {
        }

        public ForbiddenException(string message)
            : base(ApiResultStatusCode.Forbidden, HttpStatusCode.Forbidden, message)
        {
        }
    }
}
