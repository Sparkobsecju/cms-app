import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AppUser, AppUserQuery, AppUserRequest } from '@core/models/app-user.model';

/** Data access for the AppUser CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class AppUserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/appusers`;

  /** GET all users. */
  list(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: AppUserQuery): Observable<AppUser[]> {
    return this.http.post<AppUser[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single user (with assigned roles) by UserId (string PK). */
  get(userId: string): Observable<AppUser> {
    return this.http.get<AppUser>(`${this.baseUrl}/${encodeURIComponent(userId)}`);
  }

  /** POST a new user (password set server-side from the configured default). */
  create(request: AppUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.baseUrl, request);
  }

  /** PUT an existing user (UserId carried in the body; password unchanged). */
  update(request: AppUserRequest): Observable<AppUser> {
    return this.http.put<AppUser>(this.baseUrl, request);
  }

  /** DELETE a user by UserId. */
  delete(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(userId)}`);
  }

  /** POST a password reset (resets to the configured default; no body). */
  resetPassword(userId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${encodeURIComponent(userId)}/reset-password`, {});
  }
}
