import {
  Component, ChangeDetectionStrategy, inject, signal
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

  toggleSidebar(): void {
    this.sidebarCollapsed.update(v => !v);
  }

  logout(): void {
    this.authService.logout();
  }
}
