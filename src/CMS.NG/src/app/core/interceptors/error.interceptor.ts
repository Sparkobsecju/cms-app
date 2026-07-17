import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/** Fixed, user-safe message shown for every 500-class error. */
const GENERIC_ERROR = '發生未預期的錯誤，請稍後再試。 An unexpected error occurred.';

/**
 * Centralised HTTP error handling:
 *  - 401: clear the session and redirect to Login.
 *  - 500-class: show a friendly toast with a fixed generic message. The raw server `message`
 *    is deliberately NOT surfaced — a 5xx body can carry internal detail (SQL/stack fragments),
 *    so it is only logged/re-thrown, never shown to the user.
 *  - everything else (e.g. 400 validation): passed through so components/forms handle it as before.
 * The error is always re-thrown so existing per-call error handlers still run.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const messages = inject(MessageService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.clearSession();
        router.navigate(['/login']);
      } else if (error.status >= 500) {
        messages.add({
          severity: 'error',
          summary: '系統錯誤 Error',
          detail: GENERIC_ERROR,
        });
      }
      return throwError(() => error);
    }),
  );
};
