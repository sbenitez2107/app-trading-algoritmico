import { Routes } from '@angular/router';
import { ExpensesListComponent } from './expenses-list/expenses-list.component';

export const EXPENSES_ROUTES: Routes = [
  {
    path: '',
    component: ExpensesListComponent,
  },
];
