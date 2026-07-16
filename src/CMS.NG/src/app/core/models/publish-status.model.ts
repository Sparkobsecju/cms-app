/** Response model for a publishing status (發布狀態). */
export interface PublishStatus {
  /** Primary key (主代碼). Caller-supplied tinyint. */
  pkid: number;
  /** Status description (狀態說明). */
  description: string;
  /** Whether this status represents a draft (草稿). */
  isDraft: boolean;
  /** Whether this status represents published content (已發布). */
  isPublished: boolean;
  /** Whether this status represents discontinued content (已停用). */
  isDiscontinued: boolean;
}

/** Write DTO for creating/updating a publishing status. */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** Search DTO for filtering publishing statuses. */
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

/** Slim lookup row for a publishing status. */
export interface PublishStatusLookup {
  pkid: number;
  description: string;
}
