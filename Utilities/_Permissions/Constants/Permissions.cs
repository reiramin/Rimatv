namespace Utilities._Permissions.Constants
{
    public class Permissions
    {
        #region User Permissions

        public const string Admin = "Qc!Jd#";
        public const string CreateUser = "U1A#";
        public const string EditUser = "U2B!";
        public const string GetAllUsers = "U3C@";
        public const string ArchiveUser = "U4D$";
        public const string BanUser = "U5E%";
        public const string DeleteUser = "U6F^";

        #endregion

        
        #region Iptv Permissions

        public const string IptvProviderManage = "IP#M";
        public const string CreateIptvProvider = "IP1C#";
        public const string EditIptvProvider = "IP2E!";
        public const string GetAllIptvProviders = "IP3G@";
        public const string DeleteIptvProvider = "IP4D$";
        public const string SyncIptvProvider = "IP5S%";
        public const string ChannelManage = "CH#M";
        public const string GetAllChannels = "CH1G#";
        public const string EditChannel = "CH2E!";
        public const string DeleteChannel = "CH3D$";
        public const string StreamManage = "ST#M";
        public const string EditStream = "ST2E!";
        public const string DeleteStream = "ST3D$";
        public const string GetAllSyncLogs = "SL1G#";

        #endregion


        public const string EmailManage = "EMM#!";


        public const string CreateAdmin = "ADM-#C";
        public const string ResetPassword = "AD#M-RP";
        public const string ChangePassword = "A#DM-CP";
        public const string BanAdmin = "ADM#B";
        public const string DeleteAdmin = "ADM-D";
        public const string EditAdmin = "ADM##-U";
        public const string GetAdmin = "ADM#-G";
        public const string GetAllAdmins = "AD#M#-LS";


        public static readonly List<PermissionMeta> PermissionsList =
        [
            // User Permissions
            new PermissionMeta(Admin, nameof(Admin), "", ["admin"]),
            new PermissionMeta(CreateUser, nameof(CreateUser), "Permission to create new users", ["admin"]),
            new PermissionMeta(EditUser, nameof(EditUser), "Permission to edit existing users", ["admin"]),
            new PermissionMeta(GetAllUsers, nameof(GetAllUsers), "Permission to view all users", ["admin"]),
            new PermissionMeta(ArchiveUser, nameof(ArchiveUser), "Permission to archive user accounts", ["admin"]),
            new PermissionMeta(BanUser, nameof(BanUser), "Permission to ban user accounts", ["admin"]),
            new PermissionMeta(DeleteUser, nameof(DeleteUser), "Permission to delete users", ["admin"]),



            new PermissionMeta(CreateAdmin, nameof(CreateAdmin), "Create Admin", ["admin"]),
            new PermissionMeta(ResetPassword, nameof(ResetPassword), "Reset Admin Password", ["admin"]),
            new PermissionMeta(ChangePassword, nameof(ChangePassword), "Change Admin Password", ["admin"]),
            new PermissionMeta(BanAdmin, nameof(BanAdmin), "Ban/Unban Admin", ["admin"]),
            new PermissionMeta(DeleteAdmin, nameof(DeleteAdmin), "Delete Admin", ["admin"]),
            new PermissionMeta(EditAdmin, nameof(EditAdmin), "Edit Admin", ["admin"]),
            new PermissionMeta(GetAdmin, nameof(GetAdmin), "Get Admin", ["admin"]),
            new PermissionMeta(GetAllAdmins, nameof(GetAllAdmins), "Get All Admins", ["admin"]),

            // Iptv Permissions
            new PermissionMeta(IptvProviderManage, nameof(IptvProviderManage), "IPTV Provider Management", ["admin"]),
            new PermissionMeta(CreateIptvProvider, nameof(CreateIptvProvider), "Create IPTV provider", ["admin"]),
            new PermissionMeta(EditIptvProvider, nameof(EditIptvProvider), "Edit IPTV provider", ["admin"]),
            new PermissionMeta(GetAllIptvProviders, nameof(GetAllIptvProviders), "View IPTV providers", ["admin"]),
            new PermissionMeta(DeleteIptvProvider, nameof(DeleteIptvProvider), "Delete IPTV provider", ["admin"]),
            new PermissionMeta(SyncIptvProvider, nameof(SyncIptvProvider), "Trigger IPTV provider sync", ["admin"]),
            new PermissionMeta(ChannelManage, nameof(ChannelManage), "Channel Management", ["admin"]),
            new PermissionMeta(GetAllChannels, nameof(GetAllChannels), "View all channels including inactive", ["admin"]),
            new PermissionMeta(EditChannel, nameof(EditChannel), "Activate/deactivate channels", ["admin"]),
            new PermissionMeta(DeleteChannel, nameof(DeleteChannel), "Delete channels", ["admin"]),
            new PermissionMeta(StreamManage, nameof(StreamManage), "Stream Management", ["admin"]),
            new PermissionMeta(EditStream, nameof(EditStream), "Activate/deactivate streams", ["admin"]),
            new PermissionMeta(DeleteStream, nameof(DeleteStream), "Delete streams", ["admin"]),
            new PermissionMeta(GetAllSyncLogs, nameof(GetAllSyncLogs), "View sync logs", ["admin"]),
        ];


        public static readonly IEnumerable<string> AllRoles = ["admin"];
    }

    public record PermissionMeta(string Code, string Title, string Description, IEnumerable<string> Roles)
    {
        public bool IsNew { get; init; }
    }
}