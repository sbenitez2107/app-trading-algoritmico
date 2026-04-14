import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface AnalyzerRuleDto {
  id: string;
  name: string;
  description: string;
  priority: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateAnalyzerRuleDto {
  name: string;
  description: string;
  priority?: number;
}

export interface UpdateAnalyzerRuleDto {
  name?: string;
  description?: string;
  priority?: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class AnalyzerRuleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/api/analyzer-rules`;

  getAll(): Observable<AnalyzerRuleDto[]> {
    return this.http.get<AnalyzerRuleDto[]>(this.baseUrl);
  }

  create(dto: CreateAnalyzerRuleDto): Observable<AnalyzerRuleDto> {
    return this.http.post<AnalyzerRuleDto>(this.baseUrl, dto);
  }

  update(id: string, dto: UpdateAnalyzerRuleDto): Observable<AnalyzerRuleDto> {
    return this.http.patch<AnalyzerRuleDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
