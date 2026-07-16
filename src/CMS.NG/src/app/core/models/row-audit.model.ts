/** One RowAudit history entry for a record (異動紀錄). */
export interface RowAuditEntry {
  /** When the change happened (ISO date-time string). */
  dateTime: string;
  /** Acting user's UserName, or "system". */
  userName: string;
  /** "Insert" | "Update" | "Delete". */
  actionType: string;
  /** Human description (first string column, or the changed-column list for Update). */
  actionDesc: string | null;
}
