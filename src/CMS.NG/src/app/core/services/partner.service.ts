import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Partner, PartnerQuery, PartnerRequest } from '@core/models/partner.model';

/** Data access for the Partner CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class PartnerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/partners`;

  /** GET all partners. */
  list(): Observable<Partner[]> {
    return this.http.get<Partner[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: PartnerQuery): Observable<Partner[]> {
    return this.http.post<Partner[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single partner by pkid (numeric PK). */
  get(pkid: number): Observable<Partner> {
    return this.http.get<Partner>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a new partner. */
  create(request: PartnerRequest): Observable<Partner> {
    return this.http.post<Partner>(this.baseUrl, request);
  }

  /** PUT an existing partner (pkid carried in the body). */
  update(request: PartnerRequest): Observable<Partner> {
    return this.http.put<Partner>(this.baseUrl, request);
  }

  /** DELETE a partner by pkid. */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
