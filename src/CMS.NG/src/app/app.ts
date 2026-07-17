import { Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Toast } from 'primeng/toast';
import { AuthService } from '@core/services/auth.service';

interface NavChild {
  label: string;
  icon: string;
  route?: string;
}

interface NavGroup {
  label: string;
  icon: string;
  expanded?: boolean;
  /** When true, the group is only shown to users holding the Admin role. */
  adminOnly?: boolean;
  children: NavChild[];
}

/** Root shell: dark sidebar + routed content area (mirrors the UI mockups). */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ConfirmDialog, Toast],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly collapsed = signal(false);

  /** The signed-in display name, shown in the sidebar footer. */
  protected readonly userName = this.auth.userName;

  // Only 系統管理 Admin → 角色 AppRole is wired; other groups are visual placeholders.
  protected readonly navGroups = signal<NavGroup[]>([
    {
      label: '首頁管理 Home',
      icon: 'pi pi-home',
      expanded: true,
      children: [
        { label: '上稿作業 FeaturedPromoItem', icon: 'pi pi-megaphone', route: '/featured-promo-items' },
      ],
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-folder',
      expanded: true,
      children: [
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-folder', route: '/course-groups' },
        { label: '原廠 Partner', icon: 'pi pi-building', route: '/partners' },
      ],
    },
    { label: '說明會 Seminar', icon: 'pi pi-comments', children: [] },
    { label: '活動管理 Promotion', icon: 'pi pi-megaphone', children: [] },
    { label: '線上報名 Forms', icon: 'pi pi-file-edit', children: [] },
    { label: '網站資訊 WebInfo', icon: 'pi pi-globe', children: [] },
    { label: '考試中心 TestingCenter', icon: 'pi pi-verified', children: [] },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      adminOnly: true,
      children: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
        { label: '使用者 AppUser', icon: 'pi pi-user', route: '/app-users' },
      ],
    },
  ]);

  // Hide admin-only groups (系統管理 Admin) unless the signed-in user holds the Admin role, and hide
  // placeholder groups that have no wired-up children (說明會 Seminar, 活動管理 Promotion, 線上報名 Forms,
  // 網站資訊 WebInfo, 考試中心 TestingCenter) so they don't render as clickable menu items that go nowhere.
  // When a group gains real children it reappears automatically.
  protected readonly visibleGroups = computed(() =>
    this.navGroups().filter(
      (group) => group.children.length > 0 && (!group.adminOnly || this.auth.hasRole('Admin')),
    ),
  );

  protected toggleCollapsed(): void {
    this.collapsed.update((v) => !v);
  }

  protected toggleGroup(group: NavGroup): void {
    group.expanded = !group.expanded;
    this.navGroups.update((groups) => [...groups]);
  }

  /** Clears the session and returns to the Login page. */
  protected logout(): void {
    this.auth.clearSession();
    this.router.navigate(['/login']);
  }
}
