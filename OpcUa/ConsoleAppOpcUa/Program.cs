using System;
using System.Net;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace ConsoleAppOpcUa
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("开始测试。。。。。。。。");
            Console.WriteLine("请输入opc地址");
            string txt1 = Console.ReadLine();

            Console.WriteLine("请输入节点信息");
            string txt2 = Console.ReadLine();


            OPCUA(txt1,txt2);


            Console.ReadKey();


        }

        #region OPCUA
        private static async void OPCUA(string txt1,string txt2)
        {
            // 1、建立通信
            // 创建一个Session对象
            // string url = txt1;
            //string url = "opc.tcp://DESKTOP-M692MCQ:49320";
            // 通过用户名和密码创建Session
            //Session session = await SessionUserName(url);
            // 通过匿名方式创建Session


            Session session = null;
            try
            {
                 session     = await SessionAnonymous(txt1);

            }
            catch (Exception ex)
            {
                Console.WriteLine("请求失败,opc地址可能错误，错误信息：" + ex.Message);

            }
            //  Session session = await SessionAnonymous(txt1);


            // 读
            Read(session);

      

        }

        #endregion



        #region 匿名方式(最简单方式)
        static async Task<Session> SessionAnonymous(string url)
        {
            // 应用配置（包含认证、）  用到创建Session里面的
            ApplicationConfiguration appConfig = new ApplicationConfiguration()
            {
                ApplicationName = "AnonymousOPCUA",
                ApplicationType = ApplicationType.Client,
                ApplicationUri = "urn:AnonymousOPCUA",
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = -1,
                    MinSubscriptionLifetime = -1
                }
            };
            // 证书验证的对象
            var certificateValidator = new CertificateValidator();
            // 创建会话的一个校验回调
            certificateValidator.CertificateValidation += (se, ev) =>
            {
                if (ServiceResult.IsGood(ev.Error))
                    ev.Accept = true;
                else if (ev.Error.StatusCode.Code == StatusCodes.BadCertificateUntrusted)
                    ev.Accept = true;
                else
                    throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", ev.Error.Code, ev.Error.AdditionalInfo));
            };

            // 配置终结点
            //EndpointDescription description = new EndpointDescription(url);
            EndpointDescription description = CoreClientUtils.SelectEndpoint(
                url, useSecurity: false
                );
            ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, description);
            // 在连接之前，进行更新（服务节点的描述）
            // 证书是否检查终结点的匹配
            // 分配的会话名称 
            // 会话的超时时间
            // 用户ID  标识(用户名/密码)
            UserIdentity userIdentity = new UserIdentity();
            // 更简单的情况：匿名
            // 通信会话  建立一个连接
            //Opc.Ua.Client.Session;
            //Opc.Ua.Server.Session;
            Session session = await Session.Create(
                appConfig,
                endpoint,
                false, false,
                "AnonymousOPCUA",
                6000,
                userIdentity,
                new string[] { }
                );

            return session;
        }
        #endregion


    


        #region 自动认证
        /// <summary>
        /// 当用户的opc服务器有用户名和密码，但开启了匿名登陆，就是用自动认证
        /// </summary>
        /// <param name="url">opc地址</param>
        /// <returns></returns>
        static async Task<Session> SessionAutoCertificate(string url)
        {
            // 应用配置（包含认证、）  用到创建Session里面的
            ApplicationConfiguration applicationConfiguration = new ApplicationConfiguration()
            {
                ApplicationName = "AutoCerti",
                ApplicationType = ApplicationType.Client,
                //ApplicationUri = "urn:AnonymousOPCUA",
                ApplicationUri = $"urn:{Dns.GetHostName()}:AutoCerti",

                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024,
                    SuppressNonceValidationErrors = true,

                    ApplicationCertificate = new CertificateIdentifier
                    {
                        // 通过此信息生成一个认证证书（非对称加密（私钥，公钥（服务器）））
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = @"CurrentUser\AutoCerti",
                        SubjectName = "AutoCerti"
                    }
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            await applicationConfiguration.Validate(ApplicationType.Client);
            // 安全性配置
            applicationConfiguration.CertificateValidator.CertificateValidation += (se, ev) =>
            {
                if (ServiceResult.IsGood(ev.Error))
                    ev.Accept = true;
                else if (ev.Error.StatusCode.Code == StatusCodes.BadCertificateUntrusted)
                    ev.Accept = true;
                else
                    throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", ev.Error.Code, ev.Error.AdditionalInfo));
            };

            ApplicationInstance applicationInstance = new ApplicationInstance
            {
                ApplicationName = "AutoCerti",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = applicationConfiguration
            };
            // 创建生成认证证书
            await applicationInstance.CheckApplicationInstanceCertificate(false, 1024);

            // 配置终结点
            EndpointDescription description = CoreClientUtils.SelectEndpoint(
                url, useSecurity: false);
            ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, description);
            
           // UserIdentity userIdentity = new UserIdentity("OPCUA", "123456");// 用户名和密码
            Session session = await Session.Create(
                applicationConfiguration,
                endpoint,
                false, false,
                "AutoCerti",
                6000,
                null,
                new string[] { }
                );

            return session;
        }
        #endregion


        // 同步读
        static void Read(Session session)
        {
            // RequestHeader
            // MaxAge:毫秒,即将获取的数据的存在的最大时长
            // TimestampsToReturn：客户端/服务端
            // NodesToRead：需要读取的标签  NodeId     //通道 1.设备 1.标记 2
            //              ns=Namespace     为什么=2
            ReadValueId readValueId1 = new ReadValueId
            {
                NodeId = "ns=2;s=ModbusTcp Channel.测试设备.测试设备_警告_2",
                AttributeId = Attributes.Value
            };
            ReadValueId readValueId2 = new ReadValueId
            {
                NodeId = "ns=2;s=ModbusTcp Channel.测试设备.测试设备_异常_4",
                AttributeId = Attributes.Value
            };
        

            // 支持多个标签
            ReadValueIdCollection readValueIds = new ReadValueIdCollection { readValueId1,readValueId2 };


            try
            {
                session.Read(null, 0, TimestampsToReturn.Both,
                readValueIds,
                out DataValueCollection values,
                out DiagnosticInfoCollection diagnosticInfos
                );

                foreach (var item in values)
                {
                    //item.WrappedValue.TypeInfo.ValueRank=-1 ： 单值 / 1 ：一维数组 /2 ：二维数据 
                    if (item.WrappedValue.TypeInfo.ValueRank == -1)
                    {

                        Console.WriteLine("读出来的单值数据：" + item);
                    }
                       
                    else if (item.WrappedValue.TypeInfo.ValueRank == 1)
                        foreach (var vv in (UInt16[])item.WrappedValue.Value)
                        {
                            Console.WriteLine(vv);
                        }
                    // 需要知道Item是数组
                    // 进行数组的操作
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("session读值出现错误,测试失败，错误信息" + ex.Message);

            }
        }




    }
}
