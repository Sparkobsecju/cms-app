/** Response model for a course group (課程群組). */
export interface CourseGroup {
  /** Primary key (主代碼). smallint IDENTITY, database-assigned. */
  pkid: number;
  /** Group name (群組名稱). */
  description: string;
}

/** Write DTO for creating/updating a course group. */
export interface CourseGroupRequest {
  /** Ignored on create (IDENTITY); identifies the row on update. */
  pkid: number;
  description: string;
}

/** Search DTO for filtering course groups. */
export interface CourseGroupQuery {
  keyword?: string | null;
}

/** Slim lookup row for a course group. */
export interface CourseGroupLookup {
  pkid: number;
  description: string;
}
