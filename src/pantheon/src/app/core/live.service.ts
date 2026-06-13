import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { LiveEvents, LiveLogs, LiveMetric, LiveProcesses } from './models';

/**
 * Wraps the styx SignalR hub. Components subscribe to a host id and receive that
 * host's live metric/process/log events.
 */
@Injectable({ providedIn: 'root' })
export class LiveService {
  private connection?: signalR.HubConnection;
  private subscribed = new Set<number>();

  readonly metric$ = new Subject<LiveMetric>();
  readonly processes$ = new Subject<LiveProcesses>();
  readonly logs$ = new Subject<LiveLogs>();
  readonly events$ = new Subject<LiveEvents>();

  private ensureConnected(): Promise<void> {
    if (this.connection) return Promise.resolve();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('metric', (m: LiveMetric) => this.metric$.next(m));
    this.connection.on('processes', (p: LiveProcesses) => this.processes$.next(p));
    this.connection.on('logs', (l: LiveLogs) => this.logs$.next(l));
    this.connection.on('events', (e: LiveEvents) => this.events$.next(e));

    // Re-join groups after a reconnect.
    this.connection.onreconnected(() => this.subscribed.forEach(id => this.connection!.invoke('Subscribe', id)));

    return this.connection.start();
  }

  async subscribe(hostId: number): Promise<void> {
    await this.ensureConnected();
    if (this.subscribed.has(hostId)) return;
    this.subscribed.add(hostId);
    await this.connection!.invoke('Subscribe', hostId);
  }

  async unsubscribe(hostId: number): Promise<void> {
    if (!this.connection || !this.subscribed.has(hostId)) return;
    this.subscribed.delete(hostId);
    await this.connection.invoke('Unsubscribe', hostId);
  }

  get metric(): Observable<LiveMetric> { return this.metric$.asObservable(); }
  get processes(): Observable<LiveProcesses> { return this.processes$.asObservable(); }
  get logs(): Observable<LiveLogs> { return this.logs$.asObservable(); }
  get events(): Observable<LiveEvents> { return this.events$.asObservable(); }
}
