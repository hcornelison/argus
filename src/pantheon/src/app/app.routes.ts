import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'hosts/:id', loadComponent: () => import('./host-detail/host-detail.component').then(m => m.HostDetailComponent) },
  { path: 'logs', loadComponent: () => import('./logs/logs.component').then(m => m.LogsComponent) },
  { path: 'events', loadComponent: () => import('./events/events.component').then(m => m.EventsComponent) },
  { path: '**', redirectTo: '' }
];
