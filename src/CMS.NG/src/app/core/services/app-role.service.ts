import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models/app-role.model';

/** Data access for the AppRole CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class AppRoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/approles`;

  /** GET all roles. */
  list(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: AppRoleQuery): Observable<AppRole[]> {
    return this.http.post<AppRole[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single role (with assigned users) by RoleId (string PK). */
  get(roleId: string): Observable<AppRole> {
    return this.http.get<AppRole>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }

  /** POST a new role. */
  create(request: AppRoleRequest): Observable<AppRole> {
    return this.http.post<AppRole>(this.baseUrl, request);
  }

  /** PUT an existing role (RoleId carried in the body). */
  update(request: AppRoleRequest): Observable<AppRole> {
    return this.http.put<AppRole>(this.baseUrl, request);
  }

  /** DELETE a role by RoleId. */
  delete(roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }
}
