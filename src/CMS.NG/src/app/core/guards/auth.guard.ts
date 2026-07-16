import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * Blocks protected routes when there is no session token, redirecting to the Login page.
 * The Login route is not guarded, so it stays public.
 */
export const authGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
