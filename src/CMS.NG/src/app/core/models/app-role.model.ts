/** Response model for an application role (角色). */
export interface AppRole {
  /** Surrogate identity key (主代碼). */
  pkid: number;
  /** Business primary key (角色代碼). */
  roleId: string;
  /** Role display name (角色名稱). */
  roleName: string;
  /** Permission level (權限等級). */
  permissionLevel: number;
  /** Optional description (描述). */
  description?: string | null;
  /** Number of users assigned to this role (使用者數). */
  userCount: number;
  /** Assigned user ids (populated on GET by id). */
  userIds: string[];
}

/** Write DTO for creating/updating a role. */
export interface AppRoleRequest {
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description?: string | null;
  userIds: string[];
}

/** Search DTO for filtering roles. */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevel?: number | null;
}

/** Slim lookup row for an application user. */
export interface AppUserLookup {
  userId: string;
  userName: string;
}
