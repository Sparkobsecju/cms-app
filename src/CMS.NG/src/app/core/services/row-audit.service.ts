import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RowAuditEntry } from '@core/models/row-audit.model';

/** Read access to a record's Row Audit trail (異動紀錄). */
@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/rowaudit`;

  /** GET this record's audit history (newest first), filtered by table name + pkid. */
  history(tableName: string, pkid: number): Observable<RowAuditEntry[]> {
    const params = new HttpParams().set('tableName', tableName).set('pkid', pkid);
    return this.http.get<RowAuditEntry[]>(this.baseUrl, { params });
  }
}
