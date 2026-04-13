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
  runningStartedAt: string | null;
  updatedAt: string | null;
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
  1: 'Running',
  2: 'Completed'
};

export const TIMEFRAME_LABELS: Record<number, string> = {
  2: 'M15', 3: 'M30', 4: 'H1', 5: 'H4'
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

  create(dto: CreateBatchDto, file?: File, strategyCount?: number): Observable<BatchDto> {
    const fd = new FormData();
    fd.append('assetId', dto.assetId);
    fd.append('timeframe', dto.timeframe.toString());
    fd.append('buildingBlockId', dto.buildingBlockId);
    if (dto.name) fd.append('name', dto.name);
    if (file) fd.append('file', file, file.name);
    if (strategyCount !== undefined) fd.append('strategyCount', strategyCount.toString());
    return this.http.post<BatchDto>(this.base, fd);
  }

  advance(batchId: string, file?: File, strategyCount?: number): Observable<BatchDto> {
    const fd = new FormData();
    if (file) fd.append('file', file, file.name);
    if (strategyCount !== undefined) fd.append('strategyCount', strategyCount.toString());
    return this.http.post<BatchDto>(`${this.base}/${batchId}/advance`, fd);
  }

  rollbackStage(batchId: string, stageId: string): Observable<BatchDto> {
    return this.http.delete<BatchDto>(`${this.base}/${batchId}/stages/${stageId}`);
  }

  delete(batchId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${batchId}`);
  }
}
