namespace Shared.Models.Models;

public enum TaxIdType { SSN, EIN }

public enum AddressType { Primary, Secondary, Mailing, Billing, Previous }

public enum PhoneType { Mobile, Home, Work, Fax }

public enum EmailType { Personal, Work, Other }

public enum ErrorCategory { Transient, Permanent, Unknown }
