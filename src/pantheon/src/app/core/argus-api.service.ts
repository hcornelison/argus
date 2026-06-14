import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { EventLog, Host, LogLine, MetricsResponse, ProcessHistoryPoint, ProcessSnapshot } from './models';

@Injectable({ providedIn: 'root' })
export class ArgusApiService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  getHosts(): Observable<Host[]> {
    return this.http.get<Host[]>(`${this.base}/hosts`);
  }

  getHost(id: number): Observable<Host> {
    return this.http.get<Host>(`${this.base}/hosts/${id}`);
  }

  getMetrics(hostId: number, from?: Date, to?: Date): Observable<MetricsResponse> {
    return this.http.get<MetricsResponse>(`${this.base}/hosts/${hostId}/metrics`, { params: range(from, to) });
  }

  getProcessHistory(hostId: number, name: string, from?: Date, to?: Date): Observable<ProcessHistoryPoint[]> {
    let params = range(from, to);
    params = params.set('name', name);
    return this.http.get<ProcessHistoryPoint[]>(`${this.base}/hosts/${hostId}/process-history`, { params });
  }

  getProcesses(hostId: number, at?: Date): Observable<ProcessSnapshot> {
    let params = new HttpParams();
    if (at) params = params.set('at', at.toISOString());
    return this.http.get<ProcessSnapshot>(`${this.base}/hosts/${hostId}/processes`, { params });
  }

  getEventLogs(hostId: number, from?: Date, to?: Date, level?: string): Observable<EventLog[]> {
    let params = range(from, to);
    if (level) params = params.set('level', level);
    return this.http.get<EventLog[]>(`${this.base}/hosts/${hostId}/eventlogs`, { params });
  }

  getEvents(opts: { hostId?: number; level?: string; channel?: string; from?: Date; to?: Date; q?: string }): Observable<EventLog[]> {
    let params = range(opts.from, opts.to);
    if (opts.hostId != null) params = params.set('hostId', opts.hostId);
    if (opts.level) params = params.set('level', opts.level);
    if (opts.channel) params = params.set('channel', opts.channel);
    if (opts.q) params = params.set('q', opts.q);
    return this.http.get<EventLog[]>(`${this.base}/eventlogs`, { params });
  }

  getLogs(opts: { hostId?: number; filePath?: string; from?: Date; to?: Date; q?: string }): Observable<LogLine[]> {
    let params = range(opts.from, opts.to);
    if (opts.hostId != null) params = params.set('hostId', opts.hostId);
    if (opts.filePath) params = params.set('filePath', opts.filePath);
    if (opts.q) params = params.set('q', opts.q);
    return this.http.get<LogLine[]>(`${this.base}/logs`, { params });
  }

  getLogFiles(hostId?: number): Observable<string[]> {
    let params = new HttpParams();
    if (hostId != null) params = params.set('hostId', hostId);
    return this.http.get<string[]>(`${this.base}/logfiles`, { params });
  }
}

function range(from?: Date, to?: Date): HttpParams {
  let params = new HttpParams();
  if (from) params = params.set('from', from.toISOString());
  if (to) params = params.set('to', to.toISOString());
  return params;
}
