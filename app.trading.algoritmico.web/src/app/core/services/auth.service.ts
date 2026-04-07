import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap, catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  email: string;
  userName: string;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly TOKEN_KEY = 'auth_token';

  private _token = signal<string | null>(this.loadToken());
  readonly isAuthenticated = computed(() => !!this._token());

  private loadToken(): string | null {
    try {
      return localStorage.getItem(this.TOKEN_KEY);
    } catch {
      return null;
    }
  }

  getToken(): string | null {
    return this._token();
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/api/auth/login`, { email, password } as LoginRequest)
      .pipe(
        tap(response => {
          localStorage.setItem(this.TOKEN_KEY, response.accessToken);
          this._token.set(response.accessToken);
        }),
        catchError(err => throwError(() => err))
      );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    this._token.set(null);
    this.router.navigate(['/login']);
  }
}
