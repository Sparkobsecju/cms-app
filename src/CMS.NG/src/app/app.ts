import { Component, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Toast } from 'primeng/toast';

interface NavChild {
  label: string;
  icon: string;
  route?: string;
}

interface NavGroup {
  label: string;
  icon: string;
  expanded?: boolean;
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
  protected readonly collapsed = signal(false);

  // Only 系統管理 Admin → 角色 AppRole is wired; other groups are visual placeholders.
  protected readonly navGroups = signal<NavGroup[]>([
    { label: '首頁管理 Home', icon: 'pi pi-home', children: [] },
    {
      label: '課程管理 Course',
      icon: 'pi pi-folder',
      expanded: true,
      children: [
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-folder', route: '/course-groups' },
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
      children: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
        { label: '使用者 AppUser', icon: 'pi pi-user' },
      ],
    },
  ]);

  protected toggleCollapsed(): void {
    this.collapsed.update((v) => !v);
  }

  protected toggleGroup(group: NavGroup): void {
    group.expanded = !group.expanded;
    this.navGroups.update((groups) => [...groups]);
  }
}
