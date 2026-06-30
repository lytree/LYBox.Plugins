namespace LYBox.Plugin.ProDataGrid;

/// <summary>
/// 共享测试数据常量池，消除各 ViewModel 间的重复数组定义。
/// </summary>
internal static class TestData
{
    public static readonly string[] FirstNames =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨磊", "赵敏", "黄强",
        "周婷", "吴鹏", "徐明", "孙丽", "马超", "朱军", "胡雪", "郭涛",
        "林峰", "何欣", "罗杰", "梁宇", "宋佳", "唐浩", "韩冰", "冯颖"
    ];

    public static readonly string[] LastNames =
    [
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄",
        "周", "吴", "徐", "孙", "马", "朱", "胡", "郭"
    ];

    public static readonly string[] Cities =
    [
        "上海", "北京", "深圳", "杭州", "成都", "武汉", "南京", "苏州",
        "广州", "重庆", "西安", "长沙", "天津", "青岛", "大连", "厦门"
    ];

    public static readonly string[] Departments =
    [
        "研发部", "市场部", "销售部", "人力资源部", "财务部", "运营部", "法务部", "客服部",
        "产品部", "设计部", "质量部", "行政部"
    ];

    public static readonly string[] Positions =
    [
        "技术总监", "高级工程师", "初级工程师", "项目经理", "实习生",
        "部门主管", "数据分析师", "产品专员", "UI设计师", "测试工程师",
        "架构师", "运维工程师", "前端开发", "后端开发", "全栈开发"
    ];

    public static readonly string[] TaskTitles =
    [
        "设计新版首页UI原型", "修复用户登录超时Bug", "编写支付模块单元测试", "代码审查 PR #128",
        "更新API接口文档", "部署v2.3到预发布环境", "重构权限认证模块", "实现全文搜索功能",
        "优化首页加载性能", "搭建CI/CD自动化流水线", "开发数据导出功能", "修复移动端适配问题",
        "集成第三方支付SDK", "设计数据库ER图", "编写用户操作手册", "实现消息推送服务",
        "优化SQL查询性能", "开发文件上传组件", "配置灰度发布策略", "实现数据备份定时任务",
        "开发用户反馈收集模块", "修复并发数据一致性问题", "设计微服务拆分方案", "实现操作日志审计功能",
        "开发数据大屏可视化", "优化Redis缓存策略", "实现多语言国际化支持", "开发自动化回归测试脚本",
        "设计系统监控告警方案", "实现单点登录集成"
    ];

    public static readonly string[] Assignees =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨磊", "赵敏", "黄强",
        "周婷", "吴鹏", "徐明", "孙丽"
    ];

    public static readonly string[] Statuses = ["待开始", "进行中", "审核中", "已完成"];

    public static readonly string[] Priorities = ["低", "中", "高", "紧急"];

    public static readonly string[] TaskCategories =
    [
        "前端开发", "后端开发", "运维部署", "测试验证", "UI设计", "文档编写",
        "数据库", "安全审计", "性能优化", "架构设计"
    ];

    public static readonly string[] TaskDescriptions =
    [
        "需要在本次迭代结束前完成，影响后续多个模块的开发进度",
        "阻塞了其他团队成员的工作，需要优先处理",
        "低优先级维护任务，可安排在空闲时间处理",
        "客户反馈的生产环境问题，需要尽快修复",
        "技术债务清理，建议在本季度内完成",
        "来自产品经理的新功能需求，已通过评审",
        "安全扫描发现的高危漏洞，需要立即修复",
        "性能监控触发的告警，响应时间超过阈值",
        "合规审计要求的必要改动，有截止日期",
        "团队技术分享准备，需要提前准备演示材料"
    ];

    public static readonly string[] EmployeeNotes =
    [
        "团队核心成员，具备出色的领导力和沟通能力，连续三年获评优秀员工",
        "本季度绩效优秀，已获晋升资格，建议纳入人才梯队培养计划",
        "正在负责Q3关键项目的交付工作，进度可控，风险已识别",
        "远程办公中，每周三到公司参加站会和代码评审",
        "正在指导三名新入职员工，帮助他们快速融入团队",
        "已申请下月休年假，工作交接计划已提交审批",
        "跨部门协作表现突出，获季度之星，客户满意度评分最高",
        "新入职员工，目前处于试用期，需要加强业务知识学习",
        "技术分享会主讲人，每月组织一次，已累计分享12次",
        "参与开源项目贡献，公司形象大使，GitHub Star超5000",
        "已完成PMP认证，具备项目管理资质，正在准备ACP考试",
        "正在攻读在职研究生学位，研究方向为分布式系统",
        "本年度最佳新人奖候选人，入职半年已独立完成3个项目",
        "负责客户培训和技术支持工作，客户满意度达98%",
        "即将调往深圳分部工作，负责华南区技术团队建设"
    ];

    /// <summary>
    /// 根据职位计算合理薪资范围。
    /// </summary>
    public static double GetSalaryForPosition(string position, Random random)
    {
        return position switch
        {
            "技术总监" => Math.Round(random.NextDouble() * 30000 + 50000, 2),
            "架构师" => Math.Round(random.NextDouble() * 25000 + 40000, 2),
            "部门主管" => Math.Round(random.NextDouble() * 20000 + 35000, 2),
            "项目经理" => Math.Round(random.NextDouble() * 15000 + 30000, 2),
            "高级工程师" => Math.Round(random.NextDouble() * 12000 + 25000, 2),
            "全栈开发" => Math.Round(random.NextDouble() * 10000 + 22000, 2),
            "前端开发" or "后端开发" => Math.Round(random.NextDouble() * 8000 + 18000, 2),
            "UI设计师" => Math.Round(random.NextDouble() * 8000 + 16000, 2),
            "数据分析师" => Math.Round(random.NextDouble() * 7000 + 15000, 2),
            "产品专员" => Math.Round(random.NextDouble() * 6000 + 14000, 2),
            "测试工程师" or "运维工程师" => Math.Round(random.NextDouble() * 6000 + 13000, 2),
            "初级工程师" => Math.Round(random.NextDouble() * 5000 + 10000, 2),
            "实习生" => Math.Round(random.NextDouble() * 2000 + 4000, 2),
            _ => Math.Round(random.NextDouble() * 10000 + 15000, 2)
        };
    }

    /// <summary>
    /// 根据职位计算合理的绩效评分范围。
    /// </summary>
    public static int GetRatingForPosition(string position, Random random)
    {
        return position switch
        {
            "技术总监" or "架构师" => random.Next(4, 6),
            "部门主管" or "项目经理" => random.Next(3, 6),
            "实习生" => random.Next(1, 4),
            _ => random.Next(1, 6)
        };
    }
}
