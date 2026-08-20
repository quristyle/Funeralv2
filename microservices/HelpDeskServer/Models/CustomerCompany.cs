using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models;

    /// <summary>고객사</summary>
    public class CustomerCompany : BaseEntity
    {
        /// <summary>고객사 명</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 이 고객사에 속한 고객(사용자) 목록
        /// </summary>
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
        /// <summary>
        /// 이 고객사와 연결된 팀 목록 (N:N 관계)
        /// </summary>
        public ICollection<TeamCompany> TeamCompanies { get; set; } = new List<TeamCompany>();
        // public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
