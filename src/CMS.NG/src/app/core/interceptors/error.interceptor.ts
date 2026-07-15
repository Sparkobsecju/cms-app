import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/** Fallback shown when the server sends no safe message with a 500-class error. */
const GENERIC_ERROR = '發生未預期的錯誤，請稍後再試。 An unexpected error occurred.';

/**
 * Centralised HTTP error handling:
 *  - 401: clear the session and redirect to Login.
 *  - 500-class: show a friendly toast using the safe message from the response body.
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
          detail: safeMessage(error),
        });
      }
      return throwError(() => error);
    }),
  );
};

/** Reads the safe `message` string from the error body, falling back to a generic message. */
function safeMessage(error: HttpErrorResponse): string {
  const body = error.error;
  if (body && typeof body === 'object' && typeof (body as { message?: unknown }).message === 'string') {
    return (body as { message: string }).message;
  }
  return GENERIC_ERROR;
}
