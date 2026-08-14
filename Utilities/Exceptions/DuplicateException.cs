using M1Mentor.Utilities.Exceptions.Common;
using Utilities.Enums;

namespace Utilities.Exceptions
{
    public class DuplicateException : BaseException
    {
        public DuplicateException()
           : base(ApiResultStatusCode.Duplicated)
        {
        }
        public DuplicateException(string message)
            : base(ApiResultStatusCode.Duplicated, message)
        {
        }
    }
}
