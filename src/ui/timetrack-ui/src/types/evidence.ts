export interface EvidenceItem {
  id: string;
  userId: string;
  evidenceType: 'screenshot';
  appName: string;
  windowTitleHash: string | null;
  capturedAt: string;
  fileSizeBytes: number;
  thumbnailUrl: string | null;
}

export interface PagedEvidenceResponse {
  items: EvidenceItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

export interface PresignedUploadUrlResponse {
  mediaId: string;
  uploadUrl: string;
  key: string;
  expiresInSeconds: number;
}

export interface PresignedDownloadUrlResponse {
  downloadUrl: string;
  evidenceItem: {
    id: string;
    userId: string;
    evidenceType: string;
    capturedAt: string;
    appName: string;
    fileSizeBytes: number;
    thumbnailUrl: string | null;
    downloadUrl: string | null;
    createdAt: string;
  };
}

export interface BatchDownloadUrlItem {
  evidenceId: string;
  downloadUrl: string | null;
}
