import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { AnalyzerRuleService, AnalyzerRuleDto } from '../../../core/services/analyzer-rule.service';
import { AnalyzerRuleFormComponent } from './analyzer-rule-form/analyzer-rule-form.component';

@Component({
  selector: 'app-strategy-analyzer',
  standalone: true,
  imports: [CommonModule, TranslateModule, AnalyzerRuleFormComponent],
  templateUrl: './strategy-analyzer.component.html',
  styleUrl: './strategy-analyzer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StrategyAnalyzerComponent {
  private readonly service = inject(AnalyzerRuleService);

  rules = signal<AnalyzerRuleDto[]>([]);
  isLoading = signal(true);
  showModal = signal(false);
  editingRule = signal<AnalyzerRuleDto | null>(null);
  deleteTarget = signal<AnalyzerRuleDto | null>(null);

  ngOnInit(): void {
    this.loadRules();
  }

  openCreate(): void {
    this.editingRule.set(null);
    this.showModal.set(true);
  }

  openEdit(rule: AnalyzerRuleDto): void {
    this.editingRule.set(rule);
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingRule.set(null);
  }

  onSaved(): void {
    this.closeModal();
    this.loadRules();
  }

  confirmDelete(rule: AnalyzerRuleDto): void {
    this.deleteTarget.set(rule);
  }

  cancelDelete(): void {
    this.deleteTarget.set(null);
  }

  executeDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.service.delete(target.id).subscribe({
      next: () => {
        this.deleteTarget.set(null);
        this.loadRules();
      },
      error: () => this.deleteTarget.set(null)
    });
  }

  private loadRules(): void {
    this.isLoading.set(true);
    this.service.getAll().subscribe({
      next: (data) => {
        this.rules.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
