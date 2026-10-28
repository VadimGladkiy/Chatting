import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AppConfig {
  pageSize: number;
}

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private settings!: AppConfig;

  constructor(private http: HttpClient) {}

  async loadConfig(): Promise<void> {
    try {
      const config = await firstValueFrom(
        this.http.get<AppConfig>('config.json')
      );
      this.settings = config;
    } catch (error) {
      console.error('Could not load config.json:', error);
      this.settings = { pageSize: 25 };
    }
  }

  get pageSize(): number {
    return this.settings.pageSize;
  }
}
