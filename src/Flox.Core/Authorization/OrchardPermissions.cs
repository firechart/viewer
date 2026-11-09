namespace Flox.Core.Authorization;

// Names of Orchard Core's own permissions (Permission.Name), for checks that must be written on
// every layer including WASM, where Orchard's assemblies can't be referenced. Only the ones Flox
// actually needs - add more when needed. Reference: https://docs.orchardcore.net/en/latest/reference/modules/Security/Permissions/
// The server-side OrchardConstantsMirrorTests fail if a name drifts from Orchard's own.
public static class OrchardPermissions
{
    // Implies every other permission
    public const string SITE_OWNER = "SiteOwner";

    public const string ACCESS_ADMIN_PANEL = "AccessAdminPanel";
    public const string MANAGE_ADMIN_SETTINGS = "ManageAdminSettings";
    public const string MANAGE_SETTINGS = "ManageSettings";
    public const string MANAGE_FEATURES = "ManageFeatures";
    public const string MANAGE_RECIPES = "ManageRecipes";

    public const string MANAGE_ROLES = "ManageRoles";

    public const string MANAGE_USERS = "ManageUsers";
    public const string VIEW_USERS = "View Users";
    public const string LIST_USERS = "ListUsers";
    public const string EDIT_USERS = "EditUsers";
    public const string DELETE_USERS = "DeleteUsers";
    public const string ASSIGN_ROLE_TO_USERS = "AssignRoleToUsers";

    public const string VIEW_AUDIT_TRAIL = "ViewAuditTrail";
    public const string MANAGE_AUDIT_TRAIL_SETTINGS = "ManageAuditTrailSettings";
}
