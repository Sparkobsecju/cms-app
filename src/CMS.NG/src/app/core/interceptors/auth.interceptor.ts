import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
import { environment } from '@env/environment';

/**
 * Attaches the session access token as `Authorization: Bearer <token>` on requests to the API origin.
 * When there is no token (signed out), or the request targets a different origin, it goes through
 * unchanged — so the bearer token is never leaked to a third-party host.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token;
  if (!token || !isApiRequest(req.url)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

/** Whether the request URL is same-origin with the configured API base URL. */
function isApiRequest(url: string): boolean {
  try {
    const target = new URL(url, window.location.origin);
    const api = new URL(environment.apiBaseUrl, window.location.origin);
    return target.origin === api.origin;
  } catch {
    return false;
  }
}
