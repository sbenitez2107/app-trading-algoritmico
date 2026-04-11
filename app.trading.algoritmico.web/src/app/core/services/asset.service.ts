import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface AssetDto {
  id: string;
  name: string;
  symbol: string;
  createdAt: string;
}

export interface CreateAssetDto {
  name: string;
  symbol: string;
}

@Injectable({ providedIn: 'root' })
export class AssetService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/assets`;

  getAll(): Observable<AssetDto[]> {
    return this.http.get<AssetDto[]>(this.base);
  }

  create(dto: CreateAssetDto): Observable<AssetDto> {
    return this.http.post<AssetDto>(this.base, dto);
  }
}
