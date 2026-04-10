import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export type AccountType = 0 | 1; // 0 = Demo, 1 = Live
export type PlatformType = 0 | 1; // 0 = MT4, 1 = MT5

export interface TradingAccountDto {
  id: string;
  name: string;
  broker: string;
  accountType: AccountType;
  platform: PlatformType;
  accountNumber: number;
  login: number;
  server: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTradingAccountDto {
  name: string;
  broker: string;
  accountType: AccountType;
  platform: PlatformType;
  accountNumber: number;
  login: number;
  password: string;
  server: string;
  isEnabled: boolean;
}

export interface UpdateTradingAccountDto {
  name: string;
  platform: PlatformType;
  accountNumber: number;
  login: number;
  password?: string;
  server: string;
  isEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class TradingAccountService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly base = `${this.apiUrl}/api/trading-accounts`;

  getAll(broker?: string, accountType?: AccountType): Observable<TradingAccountDto[]> {
    let params = new HttpParams();
    if (broker) params = params.set('broker', broker);
    if (accountType !== undefined) params = params.set('accountType', accountType.toString());
    return this.http.get<TradingAccountDto[]>(this.base, { params });
  }

  getById(id: string): Observable<TradingAccountDto> {
    return this.http.get<TradingAccountDto>(`${this.base}/${id}`);
  }

  create(dto: CreateTradingAccountDto): Observable<TradingAccountDto> {
    return this.http.post<TradingAccountDto>(this.base, dto);
  }

  update(id: string, dto: UpdateTradingAccountDto): Observable<TradingAccountDto> {
    return this.http.put<TradingAccountDto>(`${this.base}/${id}`, dto);
  }

  toggle(id: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/toggle`, null);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
