import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * Blocks a route subtree unless the signed-in user holds the required role. This is real
 * client-side authorization, not the UX-only menu filter in {@link App}; the backend still
 * enforces the same role, so this guard is defence-in-depth, not the sole gate.
 *
 * A user without the role is sent to a non-admin landing page rather than /login (they are
 * authenticated, just not authorized). An unauthenticated user is caught earlier by {@link authGuard}.
 * The target must NOT be an admin route (or one that redirects to one, e.g. '/' → app-roles), or a
 * denied non-admin would bounce in a loop.
 */
export function roleGuard(role: string): CanActivateChildFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.hasRole(role) ? true : router.createUrlTree(['/courses']);
  };
}
