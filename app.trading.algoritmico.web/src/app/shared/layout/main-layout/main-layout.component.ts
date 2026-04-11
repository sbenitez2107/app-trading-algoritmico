import {
  Component, ChangeDetectionStrategy, inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeService } from '../../../core/services/theme.service';
import { LanguageService } from '../../../core/services/language.service';
import { PreferencesService } from '../../../core/services/preferences.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink, RouterLinkActive, TranslateModule],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MainLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly themeService = inject(ThemeService);
  private readonly languageService = inject(LanguageService);
  private readonly preferencesService = inject(PreferencesService);

  readonly appVersion = environment.version;
  sidebarCollapsed = signal(false);
  darwinexExpanded = signal(false);
  sqxExpanded = signal(false);
  readonly currentUser = this.authService.currentUser;
  readonly isDark = this.themeService.isDark;
  readonly currentLang = this.languageService.language;

  readonly userInitials = computed(() => {
    const user = this.currentUser();
    if (!user?.userName) return '?';
    return user.userName
      .split(' ')
      .slice(0, 2)
      .map(w => w[0]?.toUpperCase() ?? '')
      .join('');
  });

  toggleSidebar(): void {
    this.sidebarCollapsed.update(v => !v);
    if (this.sidebarCollapsed()) {
      this.darwinexExpanded.set(false);
      this.sqxExpanded.set(false);
    }
  }

  toggleDarwinex(): void {
    if (!this.sidebarCollapsed()) this.darwinexExpanded.update(v => !v);
  }

  toggleSqx(): void {
    if (!this.sidebarCollapsed()) this.sqxExpanded.update(v => !v);
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
    this.preferencesService.updatePreference({ theme: this.themeService.theme() });
  }

  toggleLanguage(): void {
    this.languageService.toggleLanguage();
    this.preferencesService.updatePreference({ language: this.languageService.language() });
  }

  logout(): void {
    this.authService.logout();
  }
}
