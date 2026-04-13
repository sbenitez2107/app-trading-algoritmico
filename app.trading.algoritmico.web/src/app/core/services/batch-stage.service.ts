import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface BatchStageDetailDto {
  id: string;
  stageType: number;
  status: number;
  inputCount: number;
  outputCount: number;
  notes: string | null;
  runningStartedAt: string | null;
  createdAt: string;
}

export interface UpdateBatchStageDto {
  status?: number;
  notes?: string;
  outputCount?: number;
}

@Injectable({ providedIn: 'root' })
export class BatchStageService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  getDetail(batchId: string, stageId: string): Observable<BatchStageDetailDto> {
    return this.http.get<BatchStageDetailDto>(
      `${this.apiUrl}/api/batches/${batchId}/stages/${stageId}`
    );
  }

  update(batchId: string, stageId: string, dto: UpdateBatchStageDto): Observable<BatchStageDetailDto> {
    return this.http.patch<BatchStageDetailDto>(
      `${this.apiUrl}/api/batches/${batchId}/stages/${stageId}`, dto
    );
  }
}
