import {
  Component, ChangeDetectionStrategy, inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';

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

  sidebarCollapsed = signal(false);
  darwinexExpanded = signal(false);
  readonly currentUser = this.authService.currentUser;

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
    if (this.sidebarCollapsed()) this.darwinexExpanded.set(false);
  }

  toggleDarwinex(): void {
    if (!this.sidebarCollapsed()) this.darwinexExpanded.update(v => !v);
  }

  logout(): void {
    this.authService.logout();
  }
}
