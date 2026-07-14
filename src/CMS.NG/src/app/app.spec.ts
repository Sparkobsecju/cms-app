import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideNoopAnimations(), ConfirmationService, MessageService],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand and the AppRole nav entry', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.app-name')?.textContent).toContain('UWA');
    expect(compiled.textContent).toContain('角色 AppRole');
  });

  it('toggles the sidebar collapsed state', () => {
    const fixture = TestBed.createComponent(App);
    const component = fixture.componentInstance as unknown as { collapsed: () => boolean; toggleCollapsed: () => void };
    expect(component.collapsed()).toBeFalse();
    component.toggleCollapsed();
    expect(component.collapsed()).toBeTrue();
  });
});
