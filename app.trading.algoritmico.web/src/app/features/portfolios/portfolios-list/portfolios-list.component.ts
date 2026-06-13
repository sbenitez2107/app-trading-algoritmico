import { Component, ChangeDetectionStrategy, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  PortfolioService,
  PortfolioDto,
  AccountType,
} from '../../../core/services/portfolio.service';

@Component({
  selector: 'app-portfolios-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './portfolios-list.component.html',
  styleUrl: './portfolios-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortfoliosListComponent implements OnInit {
  private readonly service = inject(PortfolioService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly AccountType = AccountType;

  broker = '';
  private portfoliosBase = '/portfolios';

  readonly portfolios = signal<PortfolioDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.broker = this.route.snapshot.data['broker'] ?? '';
    this.portfoliosBase = this.route.snapshot.data['portfoliosBase'] ?? '/portfolios';
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.service.getAll(this.broker, 1, 200).subscribe({
      next: (res) => {
        this.portfolios.set(res.items);
        this.isLoading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar los portfolios');
        this.isLoading.set(false);
      },
    });
  }

  openCreate(): void {
    this.router.navigate([this.portfoliosBase, 'new']);
  }

  open(p: PortfolioDto): void {
    this.router.navigate([this.portfoliosBase, p.id]);
  }

  remove(p: PortfolioDto, event: MouseEvent): void {
    event.stopPropagation();
    if (!confirm(`¿Eliminar el portfolio "${p.name}"?`)) return;
    this.service.delete(p.id).subscribe({
      next: () => this.portfolios.update((list) => list.filter((x) => x.id !== p.id)),
      error: () => this.error.set('Error al eliminar el portfolio'),
    });
  }

  typeLabel(t: AccountType): string {
    return t === AccountType.Live ? 'Live' : 'Demo';
  }

  formatCurrency(amount: number, currency = 'USD'): string {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount);
  }
}
