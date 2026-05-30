import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export enum ExpenseCategory {
  MentoriaImox = 0,
  ServidorHetzner = 1,
  FTMO = 2,
  WSF = 3,
  DarwinexZero = 4,
  ServidorFxvsPro = 5
}

export interface ExpenseDto {
  id: string;
  description: string;
  category: ExpenseCategory;
  amount: number;
  date: string;
  notes?: string;
  createdAt: string;
}

export interface CreateExpenseDto {
  description: string;
  category: ExpenseCategory;
  amount: number;
  date: string;
  notes?: string;
}

export interface UpdateExpenseDto {
  description: string;
  category: ExpenseCategory;
  amount: number;
  date: string;
  notes?: string;
}

export interface ExpenseMonthSummaryDto {
  year: number;
  month: number;
  totalAmount: number;
  byCategory: Record<ExpenseCategory, number>;
}

export interface ExpenseProjectionDto {
  year: number;
  month: number;
  projectedTotal: number;
  byCategory: Record<ExpenseCategory, number>;
  byPropFirm: Record<string, number>;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ExpenseService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  getAll(page = 1, pageSize = 50): Observable<PagedResult<ExpenseDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<ExpenseDto>>(
      `${this.apiUrl}/api/expenses`,
      { params }
    );
  }

  getById(id: string): Observable<ExpenseDto> {
    return this.http.get<ExpenseDto>(`${this.apiUrl}/api/expenses/${id}`);
  }

  create(dto: CreateExpenseDto): Observable<ExpenseDto> {
    return this.http.post<ExpenseDto>(`${this.apiUrl}/api/expenses`, dto);
  }

  update(id: string, dto: UpdateExpenseDto): Observable<ExpenseDto> {
    return this.http.put<ExpenseDto>(`${this.apiUrl}/api/expenses/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/expenses/${id}`);
  }

  getMonthSummary(year: number, month: number): Observable<ExpenseMonthSummaryDto> {
    const params = new HttpParams()
      .set('year', year.toString())
      .set('month', month.toString());
    return this.http.get<ExpenseMonthSummaryDto>(
      `${this.apiUrl}/api/expenses/summaries/month`,
      { params }
    );
  }

  getYearSummary(year: number): Observable<ExpenseMonthSummaryDto[]> {
    const params = new HttpParams().set('year', year.toString());
    return this.http.get<ExpenseMonthSummaryDto[]>(
      `${this.apiUrl}/api/expenses/summaries/year`,
      { params }
    );
  }

  getProjections(forecastMonths = 12): Observable<ExpenseProjectionDto[]> {
    const params = new HttpParams().set('forecastMonths', forecastMonths.toString());
    return this.http.get<ExpenseProjectionDto[]>(
      `${this.apiUrl}/api/expenses/projections`,
      { params }
    );
  }

  getCategoryTotals(): Observable<Record<ExpenseCategory, number>> {
    return this.http.get<Record<ExpenseCategory, number>>(
      `${this.apiUrl}/api/expenses/categories/totals`
    );
  }
}
