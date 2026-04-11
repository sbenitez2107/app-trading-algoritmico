import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface StrategyDto {
  id: string;
  name: string;
  pseudocode: string | null;
  sharpeRatio: number | null;
  returnDrawdownRatio: number | null;
  winRate: number | null;
  profitFactor: number | null;
  totalTrades: number | null;
  netProfit: number | null;
  maxDrawdown: number | null;
  createdAt: string;
}

export interface UpdateStrategyKpisDto {
  sharpeRatio?: number;
  returnDrawdownRatio?: number;
  winRate?: number;
  profitFactor?: number;
  totalTrades?: number;
  netProfit?: number;
  maxDrawdown?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class StrategyService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  getByStage(batchId: string, stageId: string, page = 1, pageSize = 50): Observable<PagedResult<StrategyDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<StrategyDto>>(
      `${this.apiUrl}/api/batches/${batchId}/stages/${stageId}/strategies`, { params }
    );
  }

  updateKpis(strategyId: string, dto: UpdateStrategyKpisDto): Observable<StrategyDto> {
    return this.http.patch<StrategyDto>(`${this.apiUrl}/api/strategies/${strategyId}`, dto);
  }
}
