export interface ImportJob {
  id: string;
  fileName: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  totalRows: number;
  processedRows: number;
  failedRows: number;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}

export interface ImportProgress {
  importJobId: string;
  processed: number;
  total: number;
  percentage: number;
  status: string;
  failedCount: number;
}

export interface UploadResponse {
  importJobId: string;
}
