import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap, catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../../app.config';

export interface CurrentUser {
  email: string;
  userName: string;
  roles: string[];
}

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
  preferredLanguage: string;
  preferredTheme: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly TOKEN_KEY = 'auth_token';
  private readonly USER_KEY = 'auth_user';

  private _token = signal<string | null>(this.loadToken());
  private _currentUser = signal<CurrentUser | null>(this.loadUser());

  readonly isAuthenticated = computed(() => !!this._token());
  readonly currentUser = this._currentUser.asReadonly();

  private loadToken(): string | null {
    try {
      return localStorage.getItem(this.TOKEN_KEY);
    } catch {
      return null;
    }
  }

  private loadUser(): CurrentUser | null {
    try {
      const raw = localStorage.getItem(this.USER_KEY);
      return raw ? JSON.parse(raw) : null;
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
          const user: CurrentUser = {
            email: response.email,
            userName: response.userName,
            roles: response.roles
          };
          localStorage.setItem(this.TOKEN_KEY, response.accessToken);
          localStorage.setItem(this.USER_KEY, JSON.stringify(user));
          this._token.set(response.accessToken);
          this._currentUser.set(user);
        }),
        catchError(err => throwError(() => err))
      );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this._token.set(null);
    this._currentUser.set(null);
    this.router.navigate(['/login']);
  }
}
