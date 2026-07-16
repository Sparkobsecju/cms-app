import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { MessageService } from 'primeng/api';
import { Profile } from './profile';
import { AuthService } from '@core/services/auth.service';

interface ProfileInternals {
  form: { controls: { userName: { setValue: (v: string) => void } } };
  save: () => void;
}

describe('Profile', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let messageService: jasmine.SpyObj<MessageService>;

  async function createComponent() {
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideNoopAnimations(),
        { provide: AuthService, useValue: auth },
        { provide: MessageService, useValue: messageService },
      ],
    }).compileComponents();
    return TestBed.createComponent(Profile);
  }

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['updateUserName'], {
      profile: signal({ userId: 'helen', userName: 'Helen', accessToken: 't' }),
      roles: signal(['Admin', 'Editor']),
      userName: signal('Helen'),
    });
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);
  });

  it('shows the UserId and roles read-only (only UserName is editable)', async () => {
    const fixture = await createComponent();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="user-id"]')?.textContent).toContain('helen');
    const roles = el.querySelector('[data-testid="roles"]');
    expect(roles?.textContent).toContain('Admin');
    expect(roles?.textContent).toContain('Editor');

    // Within the profile form itself, the only editable input is UserName — UserId and roles have no
    // form control (the Change Password card below is a separate form).
    const profileForm = el.querySelector('form:not(.change-password)')!;
    const inputs = profileForm.querySelectorAll('input');
    expect(inputs.length).toBe(1);
    expect(inputs[0].id).toBe('userName');
  });

  it('pre-fills the UserName field with the signed-in name', async () => {
    const fixture = await createComponent();
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#userName');
    expect(input?.value).toBe('Helen');
  });

  it('saves the trimmed UserName, refreshing the shell via AuthService', async () => {
    auth.updateUserName.and.returnValue(of({ userId: 'helen', userName: 'New Name' }));
    const fixture = await createComponent();
    const comp = fixture.componentInstance as unknown as ProfileInternals;

    comp.form.controls.userName.setValue('  New Name  ');
    comp.save();

    // AuthService.updateUserName is the seam that refreshes the shell userName + session storage.
    expect(auth.updateUserName).toHaveBeenCalledWith('New Name');
    expect(messageService.add).toHaveBeenCalled();
  });

  it('does not call the API when UserName is blank/whitespace', async () => {
    const fixture = await createComponent();
    const comp = fixture.componentInstance as unknown as ProfileInternals;

    comp.form.controls.userName.setValue('   ');
    comp.save();

    expect(auth.updateUserName).not.toHaveBeenCalled();
  });
});
