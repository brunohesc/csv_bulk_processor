import { Component, OnInit, OnDestroy, ChangeDetectorRef, NgZone, ChangeDetectionStrategy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogModule } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ApiService } from './services/api.service';
import { SignalrService } from './services/signalr.service';
import { UploadComponent } from './components/upload/upload.component';
import { ProductsTableComponent } from './components/products-table/products-table.component';
import { Product, ProductFilter } from './models/product.model';
import { ImportProgress } from './models/import.model';
import { Subscription, take } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    UploadComponent,
    ProductsTableComponent
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnInit, OnDestroy {
  selectedTab = 0;

  @ViewChild(UploadComponent) uploadComponent!: UploadComponent;
  @ViewChild(ProductsTableComponent) productsTableComponent!: ProductsTableComponent;

  // Upload state
  selectedFile: File | null = null;
  isUploading = false;
  uploadProgress = 0;
  currentJobId: string | null = null;

  // Processing state
  isProcessing = false;
  isCancelling = false;
  processingProgress = 0;
  processingStatus = '';
  processedRows = 0;
  totalRows = 0;
  failedRows = 0;

  // Products state
  products: Product[] = [];
  productsLoading = false;
  productsTotalCount = 0;
  productsFilter: ProductFilter = {
    page: 1,
    pageSize: 50
  };

  private subscriptions: Subscription[] = [];

  constructor(
    private apiService: ApiService,
    private signalrService: SignalrService,
    private snackBar: MatSnackBar,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.signalrService.startConnection();
    this.setupSignalRSubscriptions();
    this.cdr.detectChanges();
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.signalrService.stopConnection();
  }

  private setupSignalRSubscriptions(): void {
    this.subscriptions.push(
      this.signalrService.progress$.subscribe((progress: ImportProgress) => {
        console.log('Progress update received:', progress);
        this.ngZone.run(() => {
          if (progress.importJobId === this.currentJobId) {
            this.processingProgress = progress.percentage;
            this.processedRows = progress.processed;
            this.totalRows = progress.total;
            this.failedRows = progress.failedCount;
            this.processingStatus = progress.status;
            this.cdr.detectChanges();
          }
        });
      })
    );

    this.subscriptions.push(
      this.signalrService.complete$.subscribe((data) => {
        this.ngZone.run(() => {
          if (data.importJobId === this.currentJobId) {
            this.isProcessing = false;
            this.processingProgress = 100;
            this.processingStatus = 'Completed';
            this.snackBar.open(
              `Import completed! ${data.successCount} products imported, ${data.failedCount} failed.`,
              'Close',
              { duration: 5000 }
            );
            // Clear filters before loading products
            this.productsFilter = {
              page: 1,
              pageSize: 50
            };
            this.productsTableComponent?.clearFilters();
            this.loadProducts();
            this.cdr.detectChanges();
            // Delay tab switch to avoid change detection error
            setTimeout(() => {
              this.selectedTab = 1; // Switch to products tab
              this.cdr.detectChanges();
            }, 0);
          }
        });
      })
    );

    this.subscriptions.push(
      this.signalrService.error$.subscribe((data) => {
        this.ngZone.run(() => {
          if (data.importJobId === this.currentJobId) {
            this.isProcessing = false;
            this.processingStatus = 'Failed';
            this.snackBar.open(`Import failed: ${data.errorMessage}`, 'Close', { duration: 5000 });
            this.cdr.detectChanges();
          }
        });
      })
    );

    this.subscriptions.push(
      this.signalrService.cancelled$.subscribe((data) => {
        this.ngZone.run(() => {
          if (data.importJobId === this.currentJobId) {
            this.isProcessing = false;
            this.isCancelling = false;
            this.processingStatus = 'Cancelled';
            this.processedRows = data.processedCount;
            this.snackBar.open(
              `Import cancelled. All processed data has been deleted.`,
              'Close',
              { duration: 5000 }
            );
            this.loadProducts();
            this.cdr.detectChanges();
          }
        });
      })
    );
  }

  onFileSelected(file: File): void {
    this.selectedFile = file;
    this.uploadFile();
  }

  private uploadFile(): void {
    if (!this.selectedFile) return;

    this.isUploading = true;
    this.uploadProgress = 0;
    this.cdr.detectChanges();

    this.apiService.uploadFile(this.selectedFile).pipe(take(1)).subscribe({
      next: (response) => {
        this.ngZone.run(() => {
          this.currentJobId = response.importJobId;
          this.isUploading = false;
          this.uploadProgress = 100;
          this.snackBar.open('File uploaded successfully. Processing started.', 'Close', { duration: 3000 });

          // Start processing
          this.isProcessing = true;
          this.processingProgress = 0;
          this.processingStatus = 'Processing';
          this.signalrService.joinGroup(response.importJobId);
          this.cdr.detectChanges();
        });
      },
      error: (error) => {
        this.ngZone.run(() => {
          this.isUploading = false;
          this.uploadProgress = 0;
          this.cdr.detectChanges();
        });
        this.snackBar.open('Upload failed. Please try again.', 'Close', { duration: 3000 });
        console.error('Upload error:', error);
      }
    });
  }

  onUploadCancelled(): void {
    this.selectedFile = null;
    this.uploadProgress = 0;
  }

  loadProducts(): void {
    console.log('Loading products with filter:', this.productsFilter);
    this.productsLoading = true;
    this.cdr.detectChanges();
    this.apiService.getProducts(this.productsFilter).pipe(take(1)).subscribe({
      next: (result) => {
        console.log('Products loaded successfully:', result);
        this.ngZone.run(() => {
          this.products = result.items;
          this.productsTotalCount = result.totalCount;
          this.productsLoading = false;
          this.cdr.detectChanges();
        });
      },
      error: (error) => {
        console.error('Load products error:', error);
        this.ngZone.run(() => {
          this.productsLoading = false;
          this.cdr.detectChanges();
        });
        this.snackBar.open('Failed to load products.', 'Close', { duration: 3000 });
      }
    });
  }

  onProductsFilterChange(filter: ProductFilter): void {
    this.productsFilter = filter;
    this.loadProducts();
  }

  onProductsSortChange(sort: any): void {
    this.productsFilter.sortBy = sort.active;
    this.productsFilter.sortOrder = sort.direction;
    console.log(sort.direction);
    this.loadProducts();
  }

  onTabChange(index: number): void {
    this.selectedTab = index;
  }

  confirmCancel(): void {
    if (window.confirm('Are you sure you want to cancel this import? This will stop the processing and any processed data will be saved.')) {
      this.cancelImport();
    }
  }

  cancelImport(): void {
    if (!this.currentJobId) return;

    this.isCancelling = true;
    this.cdr.detectChanges();

    this.apiService.cancelImport(this.currentJobId).pipe(take(1)).subscribe({
      next: () => {
        this.ngZone.run(() => {
          this.snackBar.open('Import cancelled successfully', 'Close', { duration: 3000 });
        });
      },
      error: (error) => {
        this.ngZone.run(() => {
          this.isCancelling = false;
          this.snackBar.open('Failed to cancel import', 'Close', { duration: 3000 });
          console.error('Cancel error:', error);
          this.cdr.detectChanges();
        });
      }
    });
  }

  resetUpload(): void {
    this.selectedFile = null;
    this.uploadProgress = 0;
    this.isUploading = false;
    this.isProcessing = false;
    this.isCancelling = false;
    this.processingProgress = 0;
    this.processedRows = 0;
    this.totalRows = 0;
    this.failedRows = 0;
    this.processingStatus = '';

    if (this.currentJobId) {
      this.signalrService.leaveGroup(this.currentJobId);
      this.currentJobId = null;
    }

    // Reset the upload component to clear the selected file display
    this.uploadComponent.reset();

    this.cdr.detectChanges();
  }
}
