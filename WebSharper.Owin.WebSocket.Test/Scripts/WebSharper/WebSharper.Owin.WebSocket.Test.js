(function()
{
 var Global=this,Runtime=this.IntelliFactory.Runtime,Owin,WebSocket,Client,Control,MailboxProcessor,Concurrency,console,Html,Client1,Tags;
 Runtime.Define(Global,{
  WebSharper:{
   Owin:{
    WebSocket:{
     Test:{
      Client:{
       WS:function(endpoint)
       {
        var server;
        server=Client.ConnectTo(endpoint,MailboxProcessor.Start(function(inbox)
        {
         var loop;
         loop=function()
         {
          return Concurrency.Delay(function()
          {
           var x;
           x=inbox.Receive({
            $:0
           });
           return Concurrency.Bind(x,function(_arg1)
           {
            var rc,msg,_,a,data,a1;
            rc=_arg1[1];
            msg=_arg1[0];
            if(msg.$==3)
             {
              if(console)
               {
                console.log("Connection closed.");
               }
              _=Concurrency.Return(null);
             }
            else
             {
              if(msg.$==2)
               {
                if(console)
                 {
                  console.log("WebSocket conenction open.");
                 }
                _=Concurrency.Return(null);
               }
              else
               {
                if(msg.$==1)
                 {
                  a=msg.$0.message;
                  if(console)
                   {
                    console.log(a);
                   }
                  _=Concurrency.Return(null);
                 }
                else
                 {
                  data=msg.$0;
                  if(data.$==1)
                   {
                    a1=data.$0;
                    if(console)
                     {
                      console.log(a1);
                     }
                    _=Concurrency.Return(null);
                   }
                  else
                   {
                    _=Concurrency.Return(null);
                   }
                 }
               }
             }
            return Concurrency.Combine(_,Concurrency.Delay(function()
            {
             return loop(null);
            }));
           });
          });
         };
         return loop(null);
        },{
         $:0
        }));
        Concurrency.Start(Concurrency.Delay(function()
        {
         return Concurrency.While(function()
         {
          return true;
         },Concurrency.Delay(function()
         {
          return Concurrency.Bind(Concurrency.Sleep(1000),function()
          {
           server.mailbox.AddLast({
            $:0,
            $0:{
             $:0,
             $0:"HELLO"
            }
           });
           server.resume();
           return Concurrency.Return(null);
          });
         }));
        }),{
         $:0
        });
        return Tags.Tags().text("");
       }
      }
     }
    }
   }
  }
 });
 Runtime.OnInit(function()
 {
  Owin=Runtime.Safe(Global.WebSharper.Owin);
  WebSocket=Runtime.Safe(Owin.WebSocket);
  Client=Runtime.Safe(WebSocket.Client);
  Control=Runtime.Safe(Global.WebSharper.Control);
  MailboxProcessor=Runtime.Safe(Control.MailboxProcessor);
  Concurrency=Runtime.Safe(Global.WebSharper.Concurrency);
  console=Runtime.Safe(Global.console);
  Html=Runtime.Safe(Global.WebSharper.Html);
  Client1=Runtime.Safe(Html.Client);
  return Tags=Runtime.Safe(Client1.Tags);
 });
 Runtime.OnLoad(function()
 {
  return;
 });
}());
