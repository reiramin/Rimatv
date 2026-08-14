namespace Utilities._Permissions.Constants
{
    public class Permissions
    {
        #region User Permissions

        public const string Admin = "Qc!Jd#";
        public const string Dentist = "ald#q";
        public const string Laboratory = "rnJd#e";
        public const string CreateUser = "U1A#";
        public const string EditUser = "U2B!";
        public const string GetAllUsers = "U3C@";
        public const string ArchiveUser = "U4D$";
        public const string BanUser = "U5E%";
        public const string DeleteUser = "U6F^";
        public const string FileManage = "F#FFM";

        #endregion

        #region File Permissions

        public const string UploadFile = "UF1L#";
        public const string LoadFile = "LF3I@";
        public const string DeleteFile = "DF6X!";

        #endregion


        public const string EmailManage = "EMM#!";


        public const string AdminManage = "A#DM#!";
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
            new PermissionMeta(Dentist, nameof(Dentist), "", ["Dentist"]),
            new PermissionMeta(Laboratory, nameof(Laboratory), "", ["Laboratory"]),
            new PermissionMeta(CreateUser, nameof(CreateUser), "Permission to create new users", ["admin"]),
            new PermissionMeta(EditUser, nameof(EditUser), "Permission to edit existing users", ["admin"]),
            new PermissionMeta(GetAllUsers, nameof(GetAllUsers), "Permission to view all users", ["admin"]),
            new PermissionMeta(ArchiveUser, nameof(ArchiveUser), "Permission to archive user accounts", ["admin"]),
            new PermissionMeta(BanUser, nameof(BanUser), "Permission to ban user accounts", ["admin"]),
            new PermissionMeta(DeleteUser, nameof(DeleteUser), "Permission to delete users", ["admin"]),
            new PermissionMeta(FileManage, nameof(FileManage), "Permission to manage file", ["admin"]),

            // File Permissions
            new PermissionMeta(UploadFile, nameof(UploadFile), "Permission to upload files", ["admin"]),
            new PermissionMeta(LoadFile, nameof(LoadFile), "Permission to view files", ["admin", "user"]),
            new PermissionMeta(DeleteFile, nameof(DeleteFile), "Permission to delete files", ["admin"]),
            new PermissionMeta(AdminManage, nameof(AdminManage), "Admin Management", ["admin"]),

            new PermissionMeta(CreateAdmin, nameof(CreateAdmin), "Create Admin", ["admin"]),
            new PermissionMeta(ResetPassword, nameof(ResetPassword), "Reset Admin Password", ["admin"]),
            new PermissionMeta(ChangePassword, nameof(ChangePassword), "Change Admin Password", ["admin"]),
            new PermissionMeta(BanAdmin, nameof(BanAdmin), "Ban/Unban Admin", ["admin"]),
            new PermissionMeta(DeleteAdmin, nameof(DeleteAdmin), "Delete Admin", ["admin"]),
            new PermissionMeta(EditAdmin, nameof(EditAdmin), "Edit Admin", ["admin"]),
            new PermissionMeta(GetAdmin, nameof(GetAdmin), "Get Admin", ["admin"]),
            new PermissionMeta(GetAllAdmins, nameof(GetAllAdmins), "Get All Admins", ["admin"]),
        ];


        public static readonly IEnumerable<string> AllRoles = ["admin"];
    }

    public record PermissionMeta(string Code, string Title, string Description, IEnumerable<string> Roles)
    {
        public bool IsNew { get; init; }
    }
}