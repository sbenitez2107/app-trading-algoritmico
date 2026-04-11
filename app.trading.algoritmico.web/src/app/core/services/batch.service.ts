import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface BatchStageSummaryDto {
  id: string;
  stageType: number;
  status: number;
  inputCount: number;
  outputCount: number;
}

export interface BatchDto {
  id: string;
  name: string | null;
  assetId: string;
  assetName: string;
  assetSymbol: string;
  timeframe: number;
  buildingBlockId: string;
  buildingBlockName: string;
  stages: BatchStageSummaryDto[];
  createdAt: string;
}

export interface CreateBatchDto {
  assetId: string;
  timeframe: number;
  buildingBlockId: string;
  name?: string;
}

export const STAGE_TYPE_LABELS: Record<number, string> = {
  0: 'Builder',
  1: 'Retester',
  2: 'Optimizer',
  3: 'Demo',
  4: 'Live'
};

export const STAGE_STATUS_LABELS: Record<number, string> = {
  0: 'Pending',
  1: 'In Progress',
  2: 'Completed'
};

export const TIMEFRAME_LABELS: Record<number, string> = {
  0: 'M1', 1: 'M5', 2: 'M15', 3: 'M30',
  4: 'H1', 5: 'H4', 6: 'D1', 7: 'W1', 8: 'MN'
};

@Injectable({ providedIn: 'root' })
export class BatchService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/batches`;

  getAll(assetId?: string, timeframe?: number): Observable<BatchDto[]> {
    let params = new HttpParams();
    if (assetId) params = params.set('assetId', assetId);
    if (timeframe !== undefined) params = params.set('timeframe', timeframe.toString());
    return this.http.get<BatchDto[]>(this.base, { params });
  }

  getById(id: string): Observable<BatchDto> {
    return this.http.get<BatchDto>(`${this.base}/${id}`);
  }

  create(dto: CreateBatchDto, file: File): Observable<BatchDto> {
    const fd = new FormData();
    fd.append('assetId', dto.assetId);
    fd.append('timeframe', dto.timeframe.toString());
    fd.append('buildingBlockId', dto.buildingBlockId);
    if (dto.name) fd.append('name', dto.name);
    fd.append('file', file, file.name);
    return this.http.post<BatchDto>(this.base, fd);
  }

  advance(batchId: string, file: File): Observable<BatchDto> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return this.http.post<BatchDto>(`${this.base}/${batchId}/advance`, fd);
  }
}
