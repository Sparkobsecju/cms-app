/** Response model for a system user (使用者). Never carries a password. */
export interface AppUser {
  /** Surrogate identity key (主代碼). */
  pkid: number;
  /** Business primary key (帳號). */
  userId: string;
  /** User display name (使用者名稱). */
  userName: string;
  /** Whether the account is active (啟用狀態). */
  isActive: boolean;
  /** When the password was last set/reset (密碼更新時間), ISO datetime. Display only. */
  passwordUpdatedTime?: string | null;
  /** Number of roles assigned to this user (角色數). */
  roleCount: number;
  /** Assigned role ids (populated on GET by id). */
  roleIds: string[];
}

/** Write DTO for creating/updating a user. Password is never included. */
export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

/** Search DTO for filtering users. */
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
}

/** Slim lookup row for an application role. */
export interface AppRoleLookup {
  roleId: string;
  roleName: string;
}
