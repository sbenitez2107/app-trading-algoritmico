import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface GridPresetDto {
  id: string;
  name: string;
  visibleColumns: string[];
  columnOrder: string[];
  createdAt: string;
}

export interface CreateGridPresetDto {
  name: string;
  visibleColumns: string[];
  columnOrder: string[];
}

export interface UpdateGridPresetDto {
  visibleColumns: string[];
  columnOrder: string[];
}

@Injectable({ providedIn: 'root' })
export class GridPresetService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly base = `${this.apiUrl}/api/users/me/grid-presets`;

  getAll(): Observable<GridPresetDto[]> {
    return this.http.get<GridPresetDto[]>(this.base);
  }

  create(dto: CreateGridPresetDto): Observable<GridPresetDto> {
    return this.http.post<GridPresetDto>(this.base, dto);
  }

  update(id: string, dto: UpdateGridPresetDto): Observable<GridPresetDto> {
    return this.http.put<GridPresetDto>(`${this.base}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
