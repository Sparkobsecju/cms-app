/** Response model for a training partner (原廠). */
export interface Partner {
  /** Primary key (主代碼). smallint IDENTITY, database-assigned. */
  pkid: number;
  /** Partner name (原廠名稱). */
  name: string;
  /** Application key (應用金鑰). */
  appKey: string;
  /** Display name shown on the partner menu (選單顯示名稱). */
  nameOnPartnerMenu: string;
  /** Display name shown on the course detail page (課程頁顯示名稱). */
  nameOnCourseDetailPage: string;
  /** Display order (顯示順序). */
  displayOrder: number;
  /** Image filename (圖片檔名); nullable. */
  imageFilename?: string | null;
}

/** Write DTO for creating/updating a partner. */
export interface PartnerRequest {
  /** Ignored on create (IDENTITY); identifies the row on update. */
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename?: string | null;
}

/** Search DTO for filtering partners. */
export interface PartnerQuery {
  keyword?: string | null;
}
