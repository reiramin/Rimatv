using iptv.Domain.Collections.Base;
using MongoDB.Bson.Serialization.Attributes;
using Utilities.Attributes;

namespace iptv.Domain.Collections
{
    [MonjoCollectionName("Users")]
    public class User : BaseUser
    {
        [BsonDefaultValue(UserRole.Creator)] public UserRole Role { get; set; }
        [BsonDefaultValue(null)] public string RoleDescription { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string NickName { get; set; }
        public string PhoneNumber { get; set; }
        public string EmailAddress { get; set; }
        public string RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        
    }

    public enum UserState { Active, Ban, Archived }
    public enum UserRole { Admin, CFO, Creator}
    public enum LessonType
    {
        Mathematics,
        Physics,
        Chemistry,
        Biology,
        ForeignLanguage,
        Economics,
        Geography,
        ComputerScienceIct,
        FinanceAndAccounting,
        History,
        Psychology,
        Science,
        Interview,
        MAT,
        TMUA,
        TMUA2,
        PAT,
        ENGAA,
        NSAA,
        BMO,
        SMC,
        UKCHO,
        C3L6,
        BPHO,
        BBO,
        AMC12,
        AIME,
        G5,
        BMAT,
        ESAT,
        TOEFL
    }
}
