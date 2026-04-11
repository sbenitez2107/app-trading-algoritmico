import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface BuildingBlockDto {
  id: string;
  name: string;
  description: string | null;
  type: number;
  createdAt: string;
}

export interface BuildingBlockDetailDto extends BuildingBlockDto {
  xmlConfig: string | null;
}

export interface CreateBuildingBlockDto {
  name: string;
  description: string | null;
  type: number;
}

export const BB_TYPE_LABELS: Record<number, string> = {
  0: 'BB1 Base',
  1: 'BB2 Trend',
  2: 'BB3 Volatility',
  3: 'BB4 Reversion'
};

@Injectable({ providedIn: 'root' })
export class BuildingBlockService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/building-blocks`;

  getAll(): Observable<BuildingBlockDto[]> {
    return this.http.get<BuildingBlockDto[]>(this.base);
  }

  getById(id: string): Observable<BuildingBlockDetailDto> {
    return this.http.get<BuildingBlockDetailDto>(`${this.base}/${id}`);
  }

  create(dto: CreateBuildingBlockDto, file?: File): Observable<BuildingBlockDto> {
    const formData = this.buildFormData(dto, file);
    return this.http.post<BuildingBlockDto>(this.base, formData);
  }

  update(id: string, dto: CreateBuildingBlockDto, file?: File): Observable<BuildingBlockDto> {
    const formData = this.buildFormData(dto, file);
    return this.http.put<BuildingBlockDto>(`${this.base}/${id}`, formData);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  private buildFormData(dto: CreateBuildingBlockDto, file?: File): FormData {
    const fd = new FormData();
    fd.append('name', dto.name);
    fd.append('type', dto.type.toString());
    if (dto.description) fd.append('description', dto.description);
    if (file) fd.append('file', file, file.name);
    return fd;
  }
}
