/** Response model for a training course (課程). */
export interface Course {
  /** Primary key (主代碼). int IDENTITY, database-assigned. */
  pkid: number;
  /** Course name (課程名稱). */
  title: string;
  /** Official course name (官方課程名稱). */
  officialTitle?: string | null;
  /** Brochure code (簡介代碼). */
  courseId: string;
  /** Subject code (科目代碼). */
  prodCourseId: string;
  /** Friendly URL (友善網址). */
  friendlyUrl: string;
  /** Display order (顯示順序). */
  displayOrder: number;
  /** Partner FK (原廠). */
  partnerPkid: number;
  /** Course group FK (課程群組); nullable. */
  courseGroupPkid?: number | null;
  /** Publish status FK (上架狀態). */
  publishStatusPkid: number;
  /** Schedule on / listing date (上架日期), ISO yyyy-MM-dd. */
  scheduleOn: string;
  /** Schedule off / delisting date (下架日期), ISO yyyy-MM-dd. */
  scheduleOff: string;
  /** Hours (時數). */
  hour: number;
  /** List price (定價). */
  listPrice: number;
  /** Learning credit / points (點數). */
  learningCredit: number;
  /** Course materials (教材). */
  material?: string | null;
  /** Objective (課程目標). */
  objective?: string | null;
  /** Target audience (適合對象). */
  target?: string | null;
  /** Prerequisites (先備知識). */
  prerequisites?: string | null;
  /** Outline (課程大綱). */
  outline?: string | null;
  /** Certification / exam notes (考試／認證說明). */
  towardCertOrExam?: string | null;
  /** Note (備註). */
  note?: string | null;
  /** Other info (其他資訊). */
  otherInfo?: string | null;
  /** Whether repeat attendance is allowed (允許重聽). */
  canRepeat: boolean;
  /** Partner name (原廠), resolved via JOIN. */
  partnerName: string;
  /** Course group description (課程群組), resolved via LEFT JOIN; null when unset. */
  courseGroupDescription?: string | null;
  /** Publish status description (上架狀態), resolved via JOIN. */
  publishStatusDescription: string;
  /** Associated Certification pkids (populated on GET by id). */
  certificationPkids: number[];
  /** Associated JobCategory pkids (populated on GET by id). */
  jobCategoryPkids: number[];
}

/** Write DTO for creating/updating a course. */
export interface CourseRequest {
  /** Ignored on create (IDENTITY); identifies the row on update. */
  pkid: number;
  title: string;
  officialTitle?: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid?: number | null;
  publishStatusPkid: number;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material?: string | null;
  objective?: string | null;
  target?: string | null;
  prerequisites?: string | null;
  outline?: string | null;
  towardCertOrExam?: string | null;
  note?: string | null;
  otherInfo?: string | null;
  canRepeat: boolean;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

/** Search DTO for filtering courses. */
export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
  canRepeat?: boolean | null;
}

/** Slim lookup row for a partner (原廠). */
export interface PartnerLookup {
  pkid: number;
  name: string;
}

/** Slim lookup row for a certification (認證). */
export interface CertificationLookup {
  pkid: number;
  partnerName: string;
  title: string;
}

/** Slim lookup row for a job category (職務類別). */
export interface JobCategoryLookup {
  pkid: number;
  description: string;
}
