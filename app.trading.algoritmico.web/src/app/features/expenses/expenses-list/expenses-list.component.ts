import { Component, ChangeDetectionStrategy, OnInit, signal, inject, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { AgGridAngular, AgGridModule } from 'ag-grid-angular';
import { ColDef, GridOptions } from 'ag-grid-community';
import { ExpenseService, ExpenseDto, ExpenseCategory, PagedResult } from '../../../core/services/expense.service';
import { ExpenseFormComponent } from '../expense-form/expense-form.component';

@Component({
  selector: 'app-expenses-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AgGridModule, ExpenseFormComponent],
  templateUrl: './expenses-list.component.html',
  styleUrls: ['./expenses-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpensesListComponent implements OnInit {
  private readonly expenseService = inject(ExpenseService);
  readonly gridApi = viewChild('agGrid', { read: AgGridAngular });

  readonly expenses = signal<ExpenseDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly showModal = signal(false);
  readonly selectedExpense = signal<ExpenseDto | null>(null);
  readonly totalAmount = signal(0);

  readonly gridOptions: GridOptions = {
    rowHeight: 40,
    pagination: true,
    paginationPageSize: 50,
    suppressPaginationPanel: false,
    domLayout: 'autoHeight',
    defaultColDef: {
      resizable: true,
      sortable: true,
      filter: true,
      flex: 1,
    }
  };

  readonly categoryLabels: Record<ExpenseCategory, string> = {
    [ExpenseCategory.MentoriaImox]: 'Mentoría IMOX',
    [ExpenseCategory.ServidorHetzner]: 'Servidor Hetzner',
    [ExpenseCategory.FTMO]: 'FTMO',
    [ExpenseCategory.WSF]: 'WSF',
    [ExpenseCategory.DarwinexZero]: 'Darwinex Zero',
    [ExpenseCategory.ServidorFxvsPro]: 'Servidor fxvps.pro'
  };

  readonly columnDefs: ColDef<ExpenseDto>[] = [
    {
      field: 'date',
      headerName: 'Fecha',
      flex: 0.8,
      sortable: true,
      sort: 'desc',
      valueFormatter: (params) => this.formatDate(params.value),
    },
    {
      field: 'description',
      headerName: 'Descripción',
      flex: 1.2,
      sortable: true,
      filter: 'agTextColumnFilter',
    },
    {
      field: 'category',
      headerName: 'Categoría',
      flex: 1,
      sortable: true,
      filter: 'agSetColumnFilter',
      valueFormatter: (params) => this.getCategoryLabel(params.value),
      cellStyle: { display: 'flex', alignItems: 'center' },
    },
    {
      field: 'notes',
      headerName: 'Notas',
      flex: 1.1,
      sortable: true,
      filter: 'agTextColumnFilter',
      valueFormatter: (params) => params.value || '—',
    },
    {
      field: 'amount',
      headerName: 'Monto (USD)',
      flex: 0.9,
      sortable: true,
      valueFormatter: (params) => this.formatCurrency(params.value),
      cellStyle: { textAlign: 'right' },
      aggFunc: 'sum',
    },
    {
      field: 'id',
      headerName: 'Acciones',
      flex: 0.7,
      sortable: false,
      filter: false,
      cellRenderer: (params: any) => {
        const container = document.createElement('div');
        container.style.display = 'flex';
        container.style.gap = '8px';
        container.style.justifyContent = 'center';

        const editBtn = document.createElement('button');
        editBtn.textContent = '✏️';
        editBtn.className = 'expenses-list__btn-action';
        editBtn.addEventListener('click', () => this.openEdit(params.data));

        const deleteBtn = document.createElement('button');
        deleteBtn.textContent = '🗑️';
        deleteBtn.className = 'expenses-list__btn-action';
        deleteBtn.addEventListener('click', () => this.deleteExpense(params.data.id));

        container.appendChild(editBtn);
        container.appendChild(deleteBtn);
        return container;
      },
    },
  ];

  ngOnInit(): void {
    this.loadExpenses();
  }

  loadExpenses(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.expenseService.getAll(1, 1000).subscribe({
      next: (result) => {
        this.expenses.set(result.items);
        const total = result.items.reduce((sum, exp) => sum + exp.amount, 0);
        this.totalAmount.set(total);
        this.isLoading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar los gastos');
        this.isLoading.set(false);
      }
    });
  }

  openCreate(): void {
    this.selectedExpense.set(null);
    this.showModal.set(true);
  }

  openEdit(expense: ExpenseDto): void {
    this.selectedExpense.set(expense);
    this.showModal.set(true);
  }

  deleteExpense(id: string): void {
    if (!confirm('¿Estás seguro de que querés eliminar este gasto?')) return;
    this.expenseService.delete(id).subscribe({
      next: () => this.loadExpenses(),
      error: () => this.error.set('Error al eliminar el gasto')
    });
  }

  onSaved(): void {
    this.showModal.set(false);
    this.loadExpenses();
  }

  getCategoryLabel(category: ExpenseCategory): string {
    return this.categoryLabels[category] ?? 'Desconocido';
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return new Intl.DateTimeFormat('es-AR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }).format(date);
  }
}
