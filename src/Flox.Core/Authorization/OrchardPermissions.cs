namespace Flox.Core.Authorization;

/// <summary>
/// Names of Orchard Core's own permissions, for checks written on every layer including WASM, where Orchard's assemblies cannot be referenced. Only the ones Flox needs; the server-side OrchardConstantsMirrorTests fail if a name drifts from Orchard's own.
/// </summary>
public static class OrchardPermissions
{
    /// <summary>
    /// Entering the administration.
    /// </summary>
    public const string ACCESS_ADMIN_PANEL = "AccessAdminPanel";
    /// <summary>
    /// Managing the administration theme settings.
    /// </summary>
    public const string MANAGE_ADMIN_SETTINGS = "ManageAdminSettings";
    /// <summary>
    /// Managing site settings.
    /// </summary>
    public const string MANAGE_SETTINGS = "ManageSettings";
    /// <summary>
    /// Managing features.
    /// </summary>
    public const string MANAGE_FEATURES = "ManageFeatures";
    /// <summary>
    /// Running recipes.
    /// </summary>
    public const string MANAGE_RECIPES = "ManageRecipes";

    /// <summary>
    /// Managing roles and their permissions.
    /// </summary>
    public const string MANAGE_ROLES = "ManageRoles";

    /// <summary>
    /// Managing users.
    /// </summary>
    public const string MANAGE_USERS = "ManageUsers";
    /// <summary>
    /// Viewing a user.
    /// </summary>
    public const string VIEW_USERS = "View Users";
    /// <summary>
    /// Listing users.
    /// </summary>
    public const string LIST_USERS = "ListUsers";
    /// <summary>
    /// Editing users.
    /// </summary>
    public const string EDIT_USERS = "EditUsers";
    /// <summary>
    /// Deleting users.
    /// </summary>
    public const string DELETE_USERS = "DeleteUsers";
    /// <summary>
    /// Assigning roles to users.
    /// </summary>
    public const string ASSIGN_ROLE_TO_USERS = "AssignRoleToUsers";

    /// <summary>
    /// Viewing the audit trail.
    /// </summary>
    public const string VIEW_AUDIT_TRAIL = "ViewAuditTrail";
    /// <summary>
    /// Managing the audit trail settings.
    /// </summary>
    public const string MANAGE_AUDIT_TRAIL_SETTINGS = "ManageAuditTrailSettings";
}
