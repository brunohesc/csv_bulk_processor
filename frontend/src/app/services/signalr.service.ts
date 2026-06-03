import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { ImportProgress } from '../models/import.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection: HubConnection | null = null;
  private progressSubject = new Subject<ImportProgress>();
  private completeSubject = new Subject<{ importJobId: string; successCount: number; failedCount: number }>();
  private errorSubject = new Subject<{ importJobId: string; errorMessage: string }>();
  private cancelledSubject = new Subject<{ importJobId: string; processedCount: number }>();

  progress$ = this.progressSubject.asObservable();
  complete$ = this.completeSubject.asObservable();
  error$ = this.errorSubject.asObservable();
  cancelled$ = this.cancelledSubject.asObservable();

  startConnection(): void {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('SignalR connection started successfully'))
      .catch((err) => console.error('Error starting SignalR connection:', err));

    this.setupEventHandlers();
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.hubConnection = null;
    }
  }

  joinGroup(jobId: string): void {
    if (this.hubConnection) {
      if (this.hubConnection.state === 'Connected') {
        this.hubConnection.invoke('JoinGroup', jobId).catch((err) => console.error('Error joining group:', err));
      } else {
        console.warn('SignalR connection not ready, waiting...');
        this.hubConnection.start().then(() => {
          this.hubConnection?.invoke('JoinGroup', jobId).catch((err) => console.error('Error joining group:', err));
        }).catch((err) => console.error('Error starting connection:', err));
      }
    }
  }

  leaveGroup(jobId: string): void {
    if (this.hubConnection) {
      this.hubConnection.invoke('LeaveGroup', jobId).catch((err) => console.error('Error leaving group:', err));
    }
  }

  private setupEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('ProgressUpdate', (progress: ImportProgress) => {
      this.progressSubject.next(progress);
    });

    this.hubConnection.on('ImportComplete', (data: { importJobId: string; successCount: number; failedCount: number }) => {
      this.completeSubject.next(data);
    });

    this.hubConnection.on('ImportError', (data: { importJobId: string; errorMessage: string }) => {
      this.errorSubject.next(data);
    });

    this.hubConnection.on('ImportCancelled', (data: { importJobId: string; processedCount: number }) => {
      this.cancelledSubject.next(data);
    });
  }

  getConnectionState(): string {
    return this.hubConnection?.state || 'Disconnected';
  }
}
