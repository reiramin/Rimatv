using System.ComponentModel.DataAnnotations;
using M1Mentor.Utilities.Exceptions.Common;

namespace Utilities.Attributes
{
    public class CustomRequiredAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
        {
            return string.IsNullOrEmpty(value.ToString())
                ? throw new BadRequestException(ErrorMessage)
                : true;
        }
    }
}
